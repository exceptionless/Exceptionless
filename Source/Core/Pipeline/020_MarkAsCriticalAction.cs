#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Component;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(20)]
    public class MarkAsCriticalAction : ErrorPipelineActionBase {
        protected override bool ContinueOnError { get { return true; } }

        public override void Process(ErrorPipelineContext ctx) {
            if (ctx.StackInfo == null || !ctx.StackInfo.OccurrencesAreCritical)
                return;

            Log.Trace().Message("Marking error as critical.").Write();
            ctx.Error.MarkAsCritical();
        }
    }
}