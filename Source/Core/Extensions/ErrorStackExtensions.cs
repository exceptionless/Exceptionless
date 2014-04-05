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
    public static class ErrorStackExtensions {
        public static Stack ToProjectLocalTime(this Stack stack, Project project) {
            if (stack == null)
                return null;

            if (stack.DateFixed.HasValue)
                stack.DateFixed = TimeZoneInfo.ConvertTime(stack.DateFixed.Value, project.DefaultTimeZone());

            stack.FirstOccurrence = TimeZoneInfo.ConvertTime(stack.FirstOccurrence, project.DefaultTimeZone());
            stack.LastOccurrence = TimeZoneInfo.ConvertTime(stack.LastOccurrence, project.DefaultTimeZone());

            return stack;
        }

        public static Stack ToProjectLocalTime(this Stack stack, IProjectRepository repository) {
            if (stack == null)
                return null;

            return stack.ToProjectLocalTime(repository.GetByIdCached(stack.ProjectId));
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