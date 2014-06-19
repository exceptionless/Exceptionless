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
using Exceptionless.Core.Plugins.EventPipeline;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    public abstract class EventPipelineActionBase : PipelineActionBase<EventContext> {
        protected virtual bool IsCritical { get { return false; } }
        protected virtual string[] ErrorTags { get { return new string[0]; } }
        protected virtual string ErrorMessage { get { return null; } }

        public override bool HandleError(Exception ex, EventContext ctx) {
            string message = ErrorMessage ?? String.Format("Error processing action: {0}", GetType().Name);
            Log.Error().Project(ctx.Event.ProjectId).Message(message).Exception(ex).Write();

            if (!ctx.Event.Tags.Contains("Internal")) {
                EventBuilder b = ex.ToExceptionless()
                    .AddObject(ctx.Event)
                    .AddTags("Internal")
                    .SetUserDescription(message);

                b.AddTags(ErrorTags);

                if (IsCritical)
                    b.MarkAsCritical();

                b.Submit();
            }

            return ContinueOnError;
        }
    }
}