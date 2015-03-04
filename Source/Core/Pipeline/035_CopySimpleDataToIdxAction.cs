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

            // TODO: Do we need a pipeline action to trim keys and remove null values that may be sent by other native clients.
            foreach (string key in ctx.Event.Data.Keys.Where(k => !k.StartsWith("@")).ToArray()) {
                string field = key.Trim().ToLower().Replace(' ', '-');
                if (field.StartsWith("@") || ctx.Event.Data[key] == null)
                    continue;

                Type dataType = ctx.Event.Data[key].GetType();
                if (dataType == typeof(bool)) {
                    ctx.Event.Idx.Add(field + "-b", ctx.Event.Data[key]);
                } else if (dataType.IsNumeric()) {
                    ctx.Event.Idx.Add(field + "-n", ctx.Event.Data[key]);
                } else if (dataType == typeof(DateTime) || dataType == typeof(DateTimeOffset)) {
                    ctx.Event.Idx.Add(field + "-d", ctx.Event.Data[key]);
                } else if (dataType == typeof(string)) {
                    var input = (string)ctx.Event.Data[key];
                    if (input.Length >= 1000)
                        continue;

                    if (input.GetJsonType() != JsonType.None)
                        continue;

                    if (input[0] == '"')
                        input = input.TrimStart('"').TrimEnd('"');

                    bool value;
                    DateTimeOffset dtoValue;
                    Decimal decValue;
                    Double dblValue;
                    if (Boolean.TryParse(input, out value))
                        ctx.Event.Idx.Add(field + "-b", value);
                    else if (DateTimeOffset.TryParse(input, out dtoValue))
                        ctx.Event.Idx.Add(field + "-d", dtoValue);
                    else if (Decimal.TryParse(input, out decValue))
                        ctx.Event.Idx.Add(field + "-n", decValue);
                    else if (Double.TryParse(input, out dblValue))
                        ctx.Event.Idx.Add(field + "-n", dblValue);
                    else
                        ctx.Event.Idx.Add(field + "-s", input);
                }
            }
        }
    }
}