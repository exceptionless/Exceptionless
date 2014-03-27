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
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(10)]
    public class AssignToStackAction : ErrorPipelineActionBase {
        private readonly ErrorStackRepository _stackRepository;
        private readonly ErrorSignatureFactory _signatureFactory;

        public AssignToStackAction(ErrorStackRepository stackRepository, ErrorSignatureFactory signatureFactory) {
            _stackRepository = stackRepository;
            _signatureFactory = signatureFactory;
        }

        protected override bool IsCritical { get { return true; } }

        public override void Process(ErrorPipelineContext ctx) {
            ctx.StackingInfo = ctx.Error.GetStackingInfo(_signatureFactory);
            if (String.IsNullOrEmpty(ctx.Error.ErrorStackId)) {
                if (_stackRepository == null)
                    throw new InvalidOperationException("You must pass a non-null stackRepository parameter to the constructor.");

                Log.Trace().Message("Error did not specify an ErrorStackId.").Write();
                var signature = _signatureFactory.GetSignature(ctx.Error);
                ctx.StackingInfo = ctx.Error.GetStackingInfo();
                // Set Path to be the only thing we stack on for 404 errors
                if (ctx.Error.Is404() && ctx.Error.RequestInfo != null) {
                    Log.Trace().Message("Updating SignatureInfo for 404 error.").Write();
                    signature.SignatureInfo.Clear();
                    signature.SignatureInfo.Add("HttpMethod", ctx.Error.RequestInfo.HttpMethod);
                    signature.SignatureInfo.Add("Path", ctx.Error.RequestInfo.Path);
                    signature.RecalculateHash();
                }

                ctx.StackInfo = _stackRepository.GetErrorStackInfoBySignatureHash(ctx.Error.ProjectId, signature.SignatureHash);
                if (ctx.StackInfo == null) {
                    Log.Trace().Message("Creating new error stack.").Write();
                    ctx.IsNew = true;
                    var stack = new ErrorStack {
                        OrganizationId = ctx.Error.OrganizationId,
                        ProjectId = ctx.Error.ProjectId,
                        SignatureInfo = signature.SignatureInfo,
                        SignatureHash = signature.SignatureHash,
                        Title = ctx.StackingInfo.Message,
                        Tags = ctx.Error.Tags ?? new TagSet(),
                        TotalOccurrences = 1,
                        FirstOccurrence = ctx.Error.OccurrenceDate.UtcDateTime,
                        LastOccurrence = ctx.Error.OccurrenceDate.UtcDateTime
                    };

                    _stackRepository.Add(stack, true);
                    ctx.StackInfo = new ErrorStackInfo {
                        Id = stack.Id,
                        DateFixed = stack.DateFixed,
                        OccurrencesAreCritical = stack.OccurrencesAreCritical,
                        SignatureHash = stack.SignatureHash
                    };

                    // new 404 stack id added, invalidate 404 id cache
                    if (signature.SignatureInfo.ContainsKey("Path"))
                        _stackRepository.InvalidateNotFoundIdsCache(ctx.Error.ProjectId);
                }

                Log.Trace().Message("Updating error's ErrorStackId to: {0}", ctx.StackInfo.Id).Write();
                ctx.Error.ErrorStackId = ctx.StackInfo.Id;
            } else {
                var stack = _stackRepository.GetByIdCached(ctx.Error.ErrorStackId, true);

                // TODO: Update unit tests to work with this check.
                //if (stack == null || stack.ProjectId != error.ProjectId)
                //    throw new InvalidOperationException("Invalid ErrorStackId.");
                if (stack == null)
                    return;

                if (ctx.Error.Tags != null && ctx.Error.Tags.Count > 0) {
                    if(stack.Tags == null)
                        stack.Tags = new TagSet();

                    List<string> newTags = ctx.Error.Tags.Where(t => !stack.Tags.Contains(t)).ToList();
                    if (newTags.Count > 0) {
                        stack.Tags.AddRange(newTags);
                        _stackRepository.Update(stack);
                    }
                }

                ctx.StackInfo = new ErrorStackInfo {
                    Id = stack.Id,
                    DateFixed = stack.DateFixed,
                    OccurrencesAreCritical = stack.OccurrencesAreCritical,
                    SignatureHash = stack.SignatureHash,
                    IsHidden = stack.IsHidden
                };
            }

            // sync the fixed and hidden flags to the error occurrence
            ctx.Error.IsFixed = ctx.StackInfo.DateFixed.HasValue;
            ctx.Error.IsHidden = ctx.StackInfo.IsHidden;
        }
    }
}