#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Plugins.EventPipeline;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(10)]
    public class AssignToStackAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;
        private readonly FormattingPluginManager _pluginManager;

        public AssignToStackAction(IStackRepository stackRepository, FormattingPluginManager pluginManager) {
            _stackRepository = stackRepository;
            _pluginManager = pluginManager;
        }

        protected override bool IsCritical { get { return true; } }

        public override void Process(EventContext ctx) {
            if (String.IsNullOrEmpty(ctx.Event.StackId)) {
                if (_stackRepository == null)
                    throw new InvalidOperationException("You must pass a non-null stackRepository parameter to the constructor.");

                // only add default signature info if no other signature info has been added
                if (ctx.StackSignatureData.Count == 0) {
                    ctx.StackSignatureData.Add("Type", ctx.Event.Type);
                    if (!String.IsNullOrEmpty(ctx.Event.Source))
                        ctx.StackSignatureData.Add("Source", ctx.Event.Source);
                }

                string signatureHash = ctx.StackSignatureData.Values.Any(v => v != null) ? ctx.StackSignatureData.Values.ToSHA1() : null;
                ctx.SetProperty("__SignatureHash", signatureHash);
                ctx.Event.SummaryHtml = _pluginManager.GetEventSummaryHtml(ctx.Event);

                ctx.StackInfo = _stackRepository.GetStackInfoBySignatureHash(ctx.Event.ProjectId, signatureHash);
                if (ctx.StackInfo == null) {
                    Log.Trace().Message("Creating new error stack.").Write();
                    ctx.IsNew = true;
                    var stack = new Stack {
                        OrganizationId = ctx.Event.OrganizationId,
                        ProjectId = ctx.Event.ProjectId,
                        SignatureInfo = new SettingsDictionary(ctx.StackSignatureData),
                        SignatureHash = signatureHash,
                        Title = _pluginManager.GetStackTitle(ctx.Event),
                        SummaryHtml = _pluginManager.GetStackSummaryHtml(ctx.Event),
                        Tags = ctx.Event.Tags ?? new TagSet(),
                        TotalOccurrences = 1,
                        FirstOccurrence = ctx.Event.Date.UtcDateTime,
                        LastOccurrence = ctx.Event.Date.UtcDateTime
                    };

                    ctx.Stack = _stackRepository.Add(stack, true);
                    ctx.StackInfo = new StackInfo {
                        Id = stack.Id,
                        DateFixed = stack.DateFixed,
                        OccurrencesAreCritical = stack.OccurrencesAreCritical
                    };

                    // new 404 stack id added, invalidate 404 id cache
                    if (ctx.Event.IsNotFound())
                        _stackRepository.InvalidateNotFoundIdsCache(ctx.Event.ProjectId);
                }

                Log.Trace().Message("Updating error's ErrorStackId to: {0}", ctx.StackInfo.Id).Write();
                ctx.Event.StackId = ctx.StackInfo.Id;
            } else {
                ctx.Stack = _stackRepository.GetById(ctx.Event.StackId);

                // TODO: Update unit tests to work with this check.
                //if (stack == null || stack.ProjectId != error.ProjectId)
                //    throw new InvalidOperationException("Invalid ErrorStackId.");
                if (ctx.Stack == null)
                    return;

                if (ctx.Event.Tags != null && ctx.Event.Tags.Count > 0) {
                    if (ctx.Stack.Tags == null)
                        ctx.Stack.Tags = new TagSet();

                    List<string> newTags = ctx.Event.Tags.Where(t => !ctx.Stack.Tags.Contains(t)).ToList();
                    if (newTags.Count > 0) {
                        ctx.Stack.Tags.AddRange(newTags);
                        _stackRepository.Save(ctx.Stack);
                    }
                }

                ctx.StackInfo = new StackInfo {
                    Id = ctx.Stack.Id,
                    DateFixed = ctx.Stack.DateFixed,
                    OccurrencesAreCritical = ctx.Stack.OccurrencesAreCritical,
                    IsHidden = ctx.Stack.IsHidden
                };
            }

            // sync the fixed and hidden flags to the error occurrence
            ctx.Event.IsFixed = ctx.StackInfo.DateFixed.HasValue;
            ctx.Event.IsHidden = ctx.StackInfo.IsHidden;
        }
    }
}