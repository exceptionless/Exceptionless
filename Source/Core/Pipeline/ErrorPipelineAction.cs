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
    public abstract class ErrorPipelineActionBase : PipelineActionBase<ErrorPipelineContext> {
        protected virtual bool IsCritical { get { return false; } }
        protected virtual string[] ErrorTags { get { return new string[0]; } }
        protected virtual string ErrorMessage { get { return null; } }

        public override bool HandleError(Exception ex, ErrorPipelineContext ctx) {
            string message = ErrorMessage ?? String.Format("Error processing action: {0}", GetType().Name);
            Log.Error().Project(ctx.Error.ProjectId).Message(message).Exception(ex).Write();

            if (!ctx.Error.Tags.Contains("Internal")) {
                ErrorBuilder b = ex.ToExceptionless()
                    .AddDefaultInformation()
                    .AddObject(ctx.Error)
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