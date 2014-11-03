#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless.Core.Extensions {
    public static class StackExtensions {
        public static Stack ApplyOffset(this Stack stack, TimeSpan offset) {
            if (stack == null)
                return null;

            if (stack.DateFixed.HasValue)
                stack.DateFixed = stack.DateFixed.Value.Add(offset);

            stack.FirstOccurrence = stack.FirstOccurrence.Add(offset);
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