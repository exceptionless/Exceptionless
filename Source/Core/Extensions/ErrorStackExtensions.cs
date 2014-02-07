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
        public static ErrorStack ToProjectLocalTime(this ErrorStack errorStack, Project project) {
            if (errorStack == null)
                return null;

            if (errorStack.DateFixed.HasValue)
                errorStack.DateFixed = TimeZoneInfo.ConvertTime(errorStack.DateFixed.Value, project.DefaultTimeZone());

            errorStack.FirstOccurrence = TimeZoneInfo.ConvertTime(errorStack.FirstOccurrence, project.DefaultTimeZone());
            errorStack.LastOccurrence = TimeZoneInfo.ConvertTime(errorStack.LastOccurrence, project.DefaultTimeZone());

            return errorStack;
        }

        public static ErrorStack ToProjectLocalTime(this ErrorStack errorStack, IProjectRepository repository) {
            if (errorStack == null)
                return null;

            return errorStack.ToProjectLocalTime(repository.GetByIdCached(errorStack.ProjectId));
        }
    }
}