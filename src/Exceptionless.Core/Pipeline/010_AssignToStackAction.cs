using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(10)]
    public class AssignToStackAction : EventPipelineActionBase {
        private static readonly string StackTypeName = typeof(Stack).Name;
        private readonly IStackRepository _stackRepository;
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly IMessagePublisher _publisher;

        public AssignToStackAction(IStackRepository stackRepository, FormattingPluginManager formattingPluginManager, IMessagePublisher publisher, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackRepository = stackRepository ?? throw new ArgumentNullException(nameof(stackRepository));
            _formattingPluginManager = formattingPluginManager ?? throw new ArgumentNullException(nameof(formattingPluginManager));
            _publisher = publisher;
        }

        protected override bool IsCritical => true;

        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            var stacks = new Dictionary<string, Tuple<bool, Stack>>();
            foreach (var ctx in contexts) {
                if (String.IsNullOrEmpty(ctx.Event.StackId)) {
                    // only add default signature info if no other signature info has been added
                    if (ctx.StackSignatureData.Count == 0) {
                        ctx.StackSignatureData.AddItemIfNotEmpty("Type", ctx.Event.Type);
                        ctx.StackSignatureData.AddItemIfNotEmpty("Source", ctx.Event.Source);
                    }

                    string signatureHash = ctx.StackSignatureData.Values.ToSHA1();
                    ctx.SignatureHash = signatureHash;

                    if (stacks.TryGetValue(signatureHash, out Tuple<bool, Stack> value)) {
                        ctx.Stack = value.Item2;
                    } else {
                        ctx.Stack = await _stackRepository.GetStackBySignatureHashAsync(ctx.Event.ProjectId, signatureHash).AnyContext();
                        if (ctx.Stack != null)
                            stacks.Add(signatureHash, Tuple.Create(false, ctx.Stack));
                    }

                    if (ctx.Stack == null) {
                        _logger.LogTrace("Creating new event stack.");
                        ctx.IsNew = true;

                        string title = _formattingPluginManager.GetStackTitle(ctx.Event);
                        var stack = new Stack {
                            OrganizationId = ctx.Event.OrganizationId,
                            ProjectId = ctx.Event.ProjectId,
                            SignatureInfo = new SettingsDictionary(ctx.StackSignatureData),
                            SignatureHash = signatureHash,
                            Title = title?.Truncate(1000),
                            Tags = ctx.Event.Tags ?? new TagSet(),
                            Type = ctx.Event.Type,
                            TotalOccurrences = 1,
                            FirstOccurrence = ctx.Event.Date.UtcDateTime,
                            LastOccurrence = ctx.Event.Date.UtcDateTime,
                            IsHidden = ctx.Event.IsHidden
                        };

                        ctx.Stack = stack;
                        stacks.Add(signatureHash, Tuple.Create(true, ctx.Stack));
                    }
                } else {
                    ctx.Stack = await _stackRepository.GetByIdAsync(ctx.Event.StackId, o => o.Cache()).AnyContext();
                    if (ctx.Stack == null || ctx.Stack.ProjectId != ctx.Event.ProjectId) {
                        ctx.SetError("Invalid StackId.");
                        continue;
                    }

                    ctx.SignatureHash = ctx.Stack.SignatureHash;

                    if (!stacks.ContainsKey(ctx.Stack.SignatureHash))
                        stacks.Add(ctx.Stack.SignatureHash, Tuple.Create(false, ctx.Stack));
                    else
                        stacks[ctx.Stack.SignatureHash] = Tuple.Create(false, ctx.Stack);
                }

                if (!ctx.IsNew && ctx.Event.Tags != null && ctx.Event.Tags.Count > 0) {
                    if (ctx.Stack.Tags == null)
                        ctx.Stack.Tags = new TagSet();

                    var newTags = ctx.Event.Tags.Where(t => !ctx.Stack.Tags.Contains(t)).ToList();
                    if (newTags.Count > 0 || ctx.Stack.Tags.Count > 50 || ctx.Stack.Tags.Any(t => t.Length > 100)) {
                        ctx.Stack.Tags.AddRange(newTags);
                        ctx.Stack.Tags.RemoveExcessTags();
                        // make sure the stack gets saved
                        if (!stacks.ContainsKey(ctx.Stack.SignatureHash))
                            stacks.Add(ctx.Stack.SignatureHash, Tuple.Create(true, ctx.Stack));
                        else
                            stacks[ctx.Stack.SignatureHash] = Tuple.Create(true, stacks[ctx.Stack.SignatureHash].Item2);
                    }
                }

                ctx.Event.IsFirstOccurrence = ctx.IsNew;

                // sync the fixed and hidden flags to the error occurrence
                ctx.Event.IsFixed = ctx.Stack.DateFixed.HasValue && !ctx.Stack.IsRegressed;
                ctx.Event.IsHidden = ctx.Stack.IsHidden;
            }

            var stacksToAdd = stacks.Where(kvp => kvp.Value.Item1 && String.IsNullOrEmpty(kvp.Value.Item2.Id)).Select(kvp => kvp.Value.Item2).ToList();
            if (stacksToAdd.Count > 0) {
                await _stackRepository.AddAsync(stacksToAdd, o => o.Cache().Notifications(stacksToAdd.Count == 1)).AnyContext();
                if (stacksToAdd.Count > 1) {
                    await _publisher.PublishAsync(new EntityChanged {
                        ChangeType = ChangeType.Added,
                        Type = StackTypeName,
                        Data = {
                            { ExtendedEntityChanged.KnownKeys.OrganizationId, contexts.First().Organization.Id },
                            { ExtendedEntityChanged.KnownKeys.ProjectId, contexts.First().Project.Id }
                        }
                    }).AnyContext();
                }
            }

            var stacksToSave = stacks.Where(kvp => kvp.Value.Item1 && !String.IsNullOrEmpty(kvp.Value.Item2.Id)).Select(kvp => kvp.Value.Item2).ToList();
            if (stacksToSave.Count > 0)
                await _stackRepository.SaveAsync(stacksToSave, o => o.Cache().Notifications(false)).AnyContext(); // notification will get sent later in the update stats step

            // Set stack ids after they have been saved and created
            contexts.ForEach(ctx => {
                ctx.Event.StackId = ctx.Stack?.Id;
            });
        }

        public override Task ProcessAsync(EventContext ctx) {
            return Task.CompletedTask;
        }
    }
}