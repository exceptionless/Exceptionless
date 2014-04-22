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
using MongoDB.Bson;

namespace Exceptionless.Tests.Utility {
    internal static class EventData {
        public static IEnumerable<PersistentEvent> GenerateEvents(int count = 10, bool generateId = false, string id = null, string organizationId = null, string projectId = null, string errorStackId = null, DateTime? startDate = null, DateTime? endDate = null, int minimiumNestingLevel = 0, TimeSpan? timeZoneOffset = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateEvent(generateId, id, organizationId, projectId, errorStackId, startDate, endDate, timeZoneOffset: timeZoneOffset);
        }

        public static List<PersistentEvent> GenerateSampleEvents() {
            return new List<PersistentEvent> {
                GenerateSampleEvent(),
                GenerateSampleEvent(TestConstants.EventId2),
                GenerateSampleEvent(TestConstants.EventId7),
                GenerateSampleEvent(TestConstants.EventId8),
            };
        }

        public static PersistentEvent GenerateSampleEvent(string id = TestConstants.EventId) {
            return GenerateEvent(id: id, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);
        }

        public static PersistentEvent GenerateEvent(bool generateId = false, string id = null, string organizationId = null, string projectId = null, string stackId = null, DateTime? startDate = null, DateTime? endDate = null, DateTimeOffset? occurrenceDate = null, int nestingLevel = 0, int minimiumNestingLevel = 0, TimeSpan? timeZoneOffset = null) {
            if (!startDate.HasValue)
                startDate = DateTime.Now.AddDays(-90);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            var ev = new PersistentEvent {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : null : id,
                OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
                ProjectId = projectId.IsNullOrEmpty() ? TestConstants.ProjectIds.Random() : projectId,
                Date = occurrenceDate.HasValue ? occurrenceDate.Value : new DateTimeOffset(RandomHelper.GetDateTime(startDate, endDate), timeZoneOffset.HasValue ? timeZoneOffset.Value : TimeZoneInfo.Local.BaseUtcOffset)
            };

            if (!stackId.IsNullOrEmpty())
                ev.StackId = stackId;

            for (int i = 0; i < RandomHelper.GetRange(minimiumNestingLevel, minimiumNestingLevel + 5); i++) {
                string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                while (ev.Data.ContainsKey(key))
                    key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                ev.Data.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
            }

            for (int i = 0; i < RandomHelper.GetRange(minimiumNestingLevel, minimiumNestingLevel + 5); i++) {
                string tag = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                while (ev.Tags.Contains(tag))
                    tag = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                ev.Tags.Add(tag);
            }

            ev.SetError(GenerateError(nestingLevel, minimiumNestingLevel));

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