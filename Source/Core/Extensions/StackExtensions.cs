using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Extensions {
    public static class StackExtensions {
        public static Stack ApplyOffset(this Stack stack, TimeSpan offset) {
            if (stack == null)
                return null;

            if (stack.DateFixed.HasValue)
                stack.DateFixed = stack.DateFixed.Value.Add(offset);

            if (stack.FirstOccurrence != DateTime.MinValue)
                stack.FirstOccurrence = stack.FirstOccurrence.Add(offset);

            if (stack.LastOccurrence != DateTime.MinValue)
                stack.LastOccurrence = stack.LastOccurrence.Add(offset);

            return stack;
        }

        public static bool IsFixed(this Stack stack) {
            if (stack == null)
                return false;

            return stack.DateFixed.HasValue && !stack.IsRegressed;
        }

        public static bool Is404(this Stack stack) {
            if (stack == null || stack.SignatureInfo == null)
                return false;

            return stack.SignatureInfo.ContainsKey("HttpMethod") && stack.SignatureInfo.ContainsKey("Path");
        }
    }
}