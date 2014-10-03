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
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(10)]
    public class AssignToStackAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;
        private readonly FormattingPluginManager _formattingPluginManager;

        public AssignToStackAction(IStackRepository stackRepository, FormattingPluginManager formattingPluginManager) {
            _stackRepository = stackRepository;
            _formattingPluginManager = formattingPluginManager;
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

                ctx.Stack = _stackRepository.GetStackBySignatureHash(ctx.Event.ProjectId, signatureHash);
                if (ctx.Stack == null) {
                    Log.Trace().Message("Creating new error stack.").Write();
                    ctx.IsNew = true;
                    ctx.Event.IsFirstOccurrence = true;

                    string title = _formattingPluginManager.GetStackTitle(ctx.Event);
                    var stack = new Stack {
                        OrganizationId = ctx.Event.OrganizationId,
                        ProjectId = ctx.Event.ProjectId,
                        SignatureInfo = new SettingsDictionary(ctx.StackSignatureData),
                        SignatureHash = signatureHash,
                        Title = title != null ? title.Truncate(1000) : null,
                        Tags = ctx.Event.Tags ?? new TagSet(),
                        Type = ctx.Event.Type,
                        TotalOccurrences = 1,
                        FirstOccurrence = ctx.Event.Date.UtcDateTime,
                        LastOccurrence = ctx.Event.Date.UtcDateTime
                    };

                    ctx.Stack = _stackRepository.Add(stack, true);
                }

                Log.Trace().Message("Updating error's StackId to: {0}", ctx.Stack.Id).Write();
                ctx.Event.StackId = ctx.Stack.Id;
            } else {
                ctx.Stack = _stackRepository.GetById(ctx.Event.StackId, true);

                if (ctx.Stack == null || ctx.Stack.ProjectId != ctx.Event.ProjectId)
                    throw new ApplicationException("Invalid StackId.");

                ctx.SetProperty("__SignatureHash", ctx.Stack.SignatureHash);
            }

            if (!ctx.IsNew && ctx.Event.Tags != null && ctx.Event.Tags.Count > 0) {
                if (ctx.Stack.Tags == null)
                    ctx.Stack.Tags = new TagSet();

                List<string> newTags = ctx.Event.Tags.Where(t => !ctx.Stack.Tags.Contains(t)).ToList();
                if (newTags.Count > 0) {
                    ctx.Stack.Tags.AddRange(newTags);
                    _stackRepository.Save(ctx.Stack, true);
                }
            }

            // sync the fixed and hidden flags to the error occurrence
            ctx.Event.IsFixed = ctx.Stack.DateFixed.HasValue;
            ctx.Event.IsHidden = ctx.Stack.IsHidden;
        }
    }
}