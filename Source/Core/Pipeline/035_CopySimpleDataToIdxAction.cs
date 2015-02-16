#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(40)]
    public class CopySimpleDataToIdxAction : EventPipelineActionBase {
        public override void Process(EventContext ctx) {
            if (!ctx.Organization.HasPremiumFeatures)
                return;

            foreach (string key in ctx.Event.Data.Keys.Where(k => !k.StartsWith("@")).ToArray()) {
                Type dataType = ctx.Event.Data[key].GetType();
                if (dataType == typeof(bool)) {
                    ctx.Event.Idx.Add(key.ToLower() + "-b", ctx.Event.Data[key]);
                } else if (dataType.IsNumeric()) {
                    ctx.Event.Idx.Add(key.ToLower() + "-n", ctx.Event.Data[key]);
                } else if (dataType == typeof(DateTime) || dataType == typeof(DateTimeOffset)) {
                    ctx.Event.Idx.Add(key.ToLower() + "-d", ctx.Event.Data[key]);
                } else if (dataType == typeof(string) && ((string)ctx.Event.Data[key]).Length < 1000) {
                    ctx.Event.Idx.Add(key.ToLower() + "-s", ctx.Event.Data[key]);
                }
            }
        }
    }
}