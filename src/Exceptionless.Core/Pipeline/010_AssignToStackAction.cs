using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Repositories;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

[Priority(10)]
public class AssignToStackAction : EventPipelineActionBase
{
    private static readonly string StackTypeName = nameof(Stack);
    private readonly IStackRepository _stackRepository;
    private readonly FormattingPluginManager _formattingPluginManager;
    private readonly IMessagePublisher _publisher;
    private readonly ILockProvider _lockProvider;

    public AssignToStackAction(IStackRepository stackRepository, FormattingPluginManager formattingPluginManager, IMessagePublisher publisher, AppOptions options, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _stackRepository = stackRepository ?? throw new ArgumentNullException(nameof(stackRepository));
        _formattingPluginManager = formattingPluginManager ?? throw new ArgumentNullException(nameof(formattingPluginManager));
        _publisher = publisher;
        _lockProvider = lockProvider;
    }

    protected override bool IsCritical => true;

    public override async Task ProcessBatchAsync(ICollection<EventContext> contexts)
    {
        var stacks = new Dictionary<string, StackInfo>();
        var stacksById = new Dictionary<string, StackInfo>();

        StackInfo TrackStack(Stack stack, bool isNew = false)
        {
            if (stacksById.TryGetValue(stack.Id, out var existing))
                return existing;

            var info = new StackInfo { IsNew = isNew, ShouldSave = false, Stack = stack };
            stacksById.Add(stack.Id, info);
            return info;
        }

        foreach (var ctx in contexts)
        {
            if (String.IsNullOrEmpty(ctx.Event.StackId))
            {
                // only add default signature info if no other signature info has been added
                if (ctx.StackSignatureData.Count == 0)
                {
                    ctx.StackSignatureData.AddItemIfNotEmpty("Type", ctx.Event.Type);
                    ctx.StackSignatureData.AddItemIfNotEmpty("Source", ctx.Event.Source);
                }

                string signatureHash = ctx.StackSignatureData.Values.ToSHA1();
                ctx.SignatureHash = signatureHash;

                if (stacks.TryGetValue(signatureHash, out var value))
                {
                    ctx.Stack = value.Stack;
                }
                else
                {
                    ctx.Stack = await _stackRepository.GetStackBySignatureHashAsync(ctx.Event.ProjectId, signatureHash);
                    if (ctx.Stack is not null)
                    {
                        var info = TrackStack(ctx.Stack);
                        ctx.Stack = info.Stack;
                        stacks.Add(signatureHash, info);
                    }
                }

                // create new stack in distributed lock
                if (ctx.Stack is null)
                {
                    bool success = await _lockProvider.TryUsingAsync($"new-stack:{ctx.Event.ProjectId}:{signatureHash}", async () =>
                    {
                        // double check in case another process just created the stack
                        var newStack = await _stackRepository.GetStackBySignatureHashAsync(ctx.Event.ProjectId, signatureHash);
                        if (newStack is not null)
                        {
                            var info = TrackStack(newStack);
                            ctx.Stack = info.Stack;
                            stacks.Add(signatureHash, info);
                            return;
                        }

                        _logger.LogTrace("Creating new event stack");
                        ctx.IsNew = true;

                        string title = _formattingPluginManager.GetStackTitle(ctx.Event);
                        var stack = new Stack
                        {
                            OrganizationId = ctx.Event.OrganizationId,
                            ProjectId = ctx.Event.ProjectId,
                            SignatureInfo = new SettingsDictionary(ctx.StackSignatureData),
                            SignatureHash = signatureHash,
                            DuplicateSignature = ctx.Event.ProjectId + ":" + signatureHash,
                            Title = title.Truncate(1000),
                            Tags = ctx.Event.Tags ?? new TagSet(),
                            Type = ctx.Event.Type ?? Event.KnownTypes.Log,
                            TotalOccurrences = 1,
                            FirstOccurrence = ctx.Event.Date.UtcDateTime,
                            LastOccurrence = ctx.Event.Date.UtcDateTime
                        };

                        if (ctx.Event.Type == Event.KnownTypes.Session)
                            stack.Status = StackStatus.Ignored;

                        ctx.Stack = stack;
                        await _stackRepository.AddAsync(stack, o => o.Cache());

                        stacks.Add(signatureHash, TrackStack(stack, isNew: true));
                    }, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

                    if (!success)
                    {
                        ctx.SetError($"Unable to create new stack: project={ctx.Event.ProjectId} signature={signatureHash}");
                        continue;
                    }
                }
            }
            else
            {
                if (!stacksById.TryGetValue(ctx.Event.StackId, out var info))
                {
                    var stack = await _stackRepository.GetByIdAsync(ctx.Event.StackId, o => o.Cache());
                    if (stack is not null)
                        info = TrackStack(stack);
                }

                ctx.Stack = info?.Stack;
                if (ctx.Stack is null || ctx.Stack.ProjectId != ctx.Event.ProjectId)
                {
                    ctx.SetError("Invalid StackId.");
                    continue;
                }

                ctx.SignatureHash = ctx.Stack.SignatureHash;
                stacks[ctx.Stack.SignatureHash] = info!;
            }

            if (ctx.Stack!.Status == StackStatus.Discarded)
            {
                ctx.IsDiscarded = true;
                ctx.IsCancelled = true;
                continue;
            }

            if (ctx is { IsNew: false, Event.Tags.Count: > 0 })
            {
                ctx.Stack.Tags ??= new TagSet();
                var newTags = ctx.Event.Tags.Where(t => !ctx.Stack.Tags.Contains(t)).ToList();
                if (newTags.Count > 0 || ctx.Stack.Tags.Count > 50 || ctx.Stack.Tags.Any(t => t?.Length > 100))
                {
                    ctx.Stack.Tags.AddRange(newTags);
                    ctx.Stack.Tags.RemoveExcessTags();

                    stacksById[ctx.Stack.Id].ShouldSave = true;
                }
            }

            ctx.Event.IsFirstOccurrence = ctx.IsNew;
        }

        var addedStacks = stacksById.Values.Where(s => s.IsNew).Select(s => s.Stack).ToList();
        if (addedStacks.Count > 0)
        {
            await _publisher.PublishAsync(new EntityChanged
            {
                ChangeType = ChangeType.Added,
                Type = StackTypeName,
                Id = addedStacks.Count == 1 ? addedStacks.First().Id : null,
                Data = {
                    { ExtendedEntityChanged.KnownKeys.OrganizationId, contexts.First().Organization.Id },
                    { ExtendedEntityChanged.KnownKeys.ProjectId, contexts.First().Project.Id }
                }
            });
        }

        var stacksToSave = stacksById.Values.Where(s => s.ShouldSave).Select(s => s.Stack).ToList();
        if (stacksToSave.Count > 0)
            await _stackRepository.SaveAsync(stacksToSave, o => o.Cache().Notifications(false)); // notification will get sent later in the update stats step

        // Set stack ids after they have been saved and created
        contexts.ForEach(ctx =>
        {
            ctx.Event.StackId = ctx.Stack!.Id;
        });
    }

    public override Task ProcessAsync(EventContext ctx)
    {
        return Task.CompletedTask;
    }

    private record StackInfo
    {
        public required bool IsNew { get; init; }
        public required bool ShouldSave { get; set; }
        public required Stack Stack { get; set; }
    }
}
