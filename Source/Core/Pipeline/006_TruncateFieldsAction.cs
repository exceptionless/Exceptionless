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
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(6)]
    public class TruncateFieldsAction : EventPipelineActionBase {
        protected override bool IsCritical { get { return true; } }

        public override void Process(EventContext ctx) {
            if (ctx.Event.Tags != null)
                ctx.Event.Tags.RemoveWhere(t => String.IsNullOrEmpty(t) || t.Length > 255);

            if (ctx.Event.Message != null && ctx.Event.Message.Length > 2000)
                ctx.Event.Message = ctx.Event.Message.Truncate(2000);

            if (ctx.Event.Source != null && ctx.Event.Source.Length > 2000)
                ctx.Event.Source = ctx.Event.Source.Truncate(2000);
        }
    }
}