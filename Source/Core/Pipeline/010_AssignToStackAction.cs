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
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Pipeline {
    [Priority(10)]
    public class AssignToStackAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly IMessagePublisher _publisher;

        public AssignToStackAction(IStackRepository stackRepository, FormattingPluginManager formattingPluginManager, IMessagePublisher publisher) {
            if (stackRepository == null)
                throw new ArgumentNullException(nameof(stackRepository));
            if (formattingPluginManager == null)
                throw new ArgumentNullException(nameof(formattingPluginManager));

            _stackRepository = stackRepository;
            _formattingPluginManager = formattingPluginManager;
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

                    Tuple<bool, Stack> value;
                    if (stacks.TryGetValue(signatureHash, out value)) {
                        ctx.Stack = value.Item2;
                    } else {
                        ctx.Stack = await _stackRepository.GetStackBySignatureHashAsync(ctx.Event.ProjectId, signatureHash).AnyContext();
                        if (ctx.Stack != null)
                            stacks.Add(signatureHash, Tuple.Create(false, ctx.Stack));
                    }

                    if (ctx.Stack == null) {
                        Logger.Trace().Message("Creating new event stack.").Write();
                        ctx.IsNew = true;

                        var msi = ctx.Event.GetManualStackingInfo();
                        string title = !String.IsNullOrWhiteSpace(msi?.Title) ? msi.Title : _formattingPluginManager.GetStackTitle(ctx.Event);
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
                    ctx.Stack = await _stackRepository.GetByIdAsync(ctx.Event.StackId, true).AnyContext();
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

                    List<string> newTags = ctx.Event.Tags.Where(t => !ctx.Stack.Tags.Contains(t)).ToList();
                    if (newTags.Count > 0) {
                        ctx.Stack.Tags.AddRange(newTags);
                        // make sure the stack gets saved
                        if (!stacks.ContainsKey(ctx.Stack.SignatureHash))
                            stacks.Add(ctx.Stack.SignatureHash, Tuple.Create(true, ctx.Stack));
                        else
                            stacks[ctx.Stack.SignatureHash] = Tuple.Create(true, stacks[ctx.Stack.SignatureHash].Item2);
                    }
                }

                ctx.Event.IsFirstOccurrence = ctx.IsNew;

                // sync the fixed and hidden flags to the error occurrence
                ctx.Event.IsFixed = ctx.Stack.DateFixed.HasValue;
                ctx.Event.IsHidden = ctx.Stack.IsHidden;
            }

            var stacksToAdd = stacks.Where(kvp => kvp.Value.Item1 && String.IsNullOrEmpty(kvp.Value.Item2.Id)).Select(kvp => kvp.Value.Item2).ToList();
            if (stacksToAdd.Count > 0) {
                await _stackRepository.AddAsync(stacksToAdd, true, sendNotification: stacksToAdd.Count == 1).AnyContext();
                if (stacksToAdd.Count > 1)
                    await _publisher.PublishAsync(new ExtendedEntityChanged { ChangeType = ChangeType.Added, Type = typeof(Stack).Name, OrganizationId = contexts.First().Organization.Id, ProjectId = contexts.First().Project.Id }).AnyContext();
            }

            var stacksToSave = stacks.Where(kvp => kvp.Value.Item1 && !String.IsNullOrEmpty(kvp.Value.Item2.Id)).Select(kvp => kvp.Value.Item2).ToList();
            if (stacksToSave.Count > 0)
                await _stackRepository.SaveAsync(stacksToSave, true, sendNotification: false).AnyContext(); // notification will get sent later in the update stats step

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
