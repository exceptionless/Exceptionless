#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Helpers;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Tests.Utility {
    internal static class EventData {
        public static IEnumerable<PersistentEvent> GenerateEvents(int count = 10, string organizationId = null, string projectId = null, string stackId = null, DateTime? startDate = null, DateTime? endDate = null, int minimiumNestingLevel = 0, TimeSpan? timeZoneOffset = null, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false) {
            for (int i = 0; i < count; i++)
                yield return GenerateEvent(organizationId, projectId, stackId, startDate, endDate, timeZoneOffset: timeZoneOffset, generateTags: generateTags, generateData: generateData, isFixed: isFixed, isHidden: isHidden);
        }

        public static PersistentEvent GenerateSampleEvent() {
            return GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);
        }

        public static PersistentEvent GenerateEvent(string organizationId = null, string projectId = null, string stackId = null, DateTime? startDate = null, DateTime? endDate = null, DateTimeOffset? occurrenceDate = null, int nestingLevel = 0, int minimiumNestingLevel = 0, TimeSpan? timeZoneOffset = null, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false)
        {
            if (!startDate.HasValue || startDate > DateTime.Now.AddHours(1))
                startDate = DateTime.Now.AddDays(-30);
            if (!endDate.HasValue || endDate > DateTime.Now.AddHours(1))
                endDate = DateTime.Now;

            var ev = new PersistentEvent {
                OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
                ProjectId = projectId.IsNullOrEmpty() ? TestConstants.ProjectIds.Random() : projectId,
                Date = occurrenceDate.HasValue ? occurrenceDate.Value : new DateTimeOffset(RandomHelper.GetDateTime(startDate, endDate), timeZoneOffset.HasValue ? timeZoneOffset.Value : TimeZoneInfo.Local.BaseUtcOffset),
                IsFixed = isFixed,
                IsHidden = isHidden
            };

            if (!stackId.IsNullOrEmpty())
                ev.StackId = stackId;

            if (generateData) {
                for (int i = 0; i < RandomHelper.GetRange(minimiumNestingLevel, minimiumNestingLevel + 5); i++) {
                    string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                    while (ev.Data.ContainsKey(key))
                        key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                    ev.Data.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
                }
            }

            if (generateTags) {
                for (int i = 0; i < RandomHelper.GetRange(minimiumNestingLevel, minimiumNestingLevel + 5); i++) {
                    string tag = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                    while (ev.Tags.Contains(tag))
                        tag = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                    ev.Tags.Add(tag);
                }
            }

            ev.Type = Event.KnownTypes.Error;
            ev.Data[Event.KnownDataKeys.Error] = GenerateError(nestingLevel, minimiumNestingLevel);

            return ev;
        }

        private static Error GenerateError(int nestingLevel = 0, int minimiumNestingLevel = 0) {
            var error = new Error();
            error.Message = @"Generated exception message.";
            error.Type = TestConstants.ExceptionTypes.Random();
            if (RandomHelper.GetBool())
                error.Code = RandomHelper.GetRange(-234523453, 98690899).ToString();

            for (int i = 0; i < RandomHelper.GetRange(minimiumNestingLevel, minimiumNestingLevel + 5); i++) {
                string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                while (error.Data.ContainsKey(key))
                    key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                error.Data.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
            }

            var stack = new StackFrameCollection();
            for (int i = 0; i < RandomHelper.GetRange(1, 10); i++)
                stack.Add(GenerateStackFrame());
            error.StackTrace = stack;

            if (minimiumNestingLevel > 0 || (nestingLevel < 5 && RandomHelper.GetBool()))
                error.Inner = GenerateError(nestingLevel + 1);

            return error;
        }

        private static StackFrame GenerateStackFrame() {
            return new StackFrame {
                DeclaringNamespace = TestConstants.Namespaces.Random(),
                DeclaringType = TestConstants.TypeNames.Random(),
                Name = TestConstants.MethodNames.Random(),
                Parameters = new ParameterCollection {
                    new Parameter {
                        Type = "String",
                        Name = "path"
                    }
                }
            };
        }
    }
}