using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;

namespace OpenTelemetry.Internal {
    internal static class EventSourceEventFormatter {
        private static readonly object[] EmptyPayload = Array.Empty<object>();

        public static string Format(EventWrittenEventArgs eventData) {
            var payloadCollection = eventData.Payload.ToArray() ?? EmptyPayload;

            ProcessPayloadArray(payloadCollection);

            if (eventData.Message != null) {
                try {
                    return string.Format(CultureInfo.InvariantCulture, eventData.Message, payloadCollection);
                }
                catch (FormatException) {
                }
            }

            var stringBuilder = StringBuilderPool.Instance.Get();

            try {
                stringBuilder.Append(eventData.EventName);

                if (!string.IsNullOrWhiteSpace(eventData.Message)) {
                    stringBuilder.AppendLine();
                    stringBuilder.Append(nameof(eventData.Message)).Append(" = ").Append(eventData.Message);
                }

                if (eventData.PayloadNames != null) {
                    for (int i = 0; i < eventData.PayloadNames.Count; i++) {
                        stringBuilder.AppendLine();
                        stringBuilder.Append(eventData.PayloadNames[i]).Append(" = ").Append(payloadCollection[i]);
                    }
                }

                return stringBuilder.ToString();
            }
            finally {
                StringBuilderPool.Instance.Return(stringBuilder);
            }
        }

        private static void ProcessPayloadArray(object[] payloadArray) {
            for (int i = 0; i < payloadArray.Length; i++) {
                payloadArray[i] = FormatValue(payloadArray[i]);
            }
        }

        private static object FormatValue(object o) {
            if (o is byte[] bytes) {
                var stringBuilder = StringBuilderPool.Instance.Get();

                try {
                    foreach (byte b in bytes) {
                        stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0:X2}", b);
                    }

                    return stringBuilder.ToString();
                }
                finally {
                    StringBuilderPool.Instance.Return(stringBuilder);
                }
            }

            return o;
        }
    }
}