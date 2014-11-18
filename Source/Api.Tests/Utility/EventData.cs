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
using System.Linq;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Helpers;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Tests.Utility {
    internal static class EventData {
        public static IEnumerable<PersistentEvent> GenerateEvents(int count = 10, string[] organizationIds = null, string[] projectIds = null, string[] stackIds = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, int maxErrorNestingLevel = 3, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false, string[] referenceIds = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateEvent(organizationIds, projectIds, stackIds, startDate, endDate, generateTags: generateTags, generateData: generateData, isFixed: isFixed, isHidden: isHidden, maxErrorNestingLevel: maxErrorNestingLevel, referenceIds: referenceIds);
        }

        public static IEnumerable<PersistentEvent> GenerateEvents(int count = 10, string organizationId = null, string projectId = null, string stackId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, int maxErrorNestingLevel = 3, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false, string referenceId = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateEvent(organizationId, projectId, stackId, startDate, endDate, generateTags: generateTags, generateData: generateData, isFixed: isFixed, isHidden: isHidden, maxErrorNestingLevel: maxErrorNestingLevel, referenceId: referenceId);
        }

        public static PersistentEvent GenerateSampleEvent() {
            return GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, maxErrorNestingLevel: 4);
        }

        public static PersistentEvent GenerateEvent(string organizationId = null, string projectId = null, string stackId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, DateTimeOffset? occurrenceDate = null, int maxErrorNestingLevel = 0, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false, string referenceId = null) {
            return GenerateEvent(
                    organizationId != null ? new[] { organizationId } : null,
                    projectId != null ? new[] { projectId } : null,
                    stackId != null ? new[] { stackId } : null,
                    startDate, endDate, occurrenceDate, maxErrorNestingLevel, generateTags, generateData, isFixed, isHidden,
                    referenceId != null ? new[] { referenceId } : null
                );
        }

        public static PersistentEvent GenerateEvent(string[] organizationIds = null, string[] projectIds = null, string[] stackIds = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, DateTimeOffset? occurrenceDate = null, int maxErrorNestingLevel = 0, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false, string[] referenceIds = null) {
            if (!startDate.HasValue || startDate > DateTimeOffset.Now.AddHours(1))
                startDate = DateTimeOffset.Now.AddDays(-30);
            if (!endDate.HasValue || endDate > DateTimeOffset.Now.AddHours(1))
                endDate = DateTimeOffset.Now;

            var ev = new PersistentEvent {
                OrganizationId = organizationIds.Random(TestConstants.OrganizationId),
                ProjectId = projectIds.Random(TestConstants.ProjectId),
                ReferenceId = referenceIds.Random(),
                Date = occurrenceDate.HasValue ? occurrenceDate.Value : RandomHelper.GetDateTimeOffset(startDate, endDate),
                IsFixed = isFixed,
                IsHidden = isHidden,
                StackId = stackIds.Random()
            };

            if (generateData) {
                for (int i = 0; i < RandomHelper.GetRange(1, 5); i++) {
                    string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 10));
                    while (ev.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                        key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                    ev.Data.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
                }
            }

            if (generateTags) {
                for (int i = 0; i < RandomHelper.GetRange(1, 3); i++) {
                    string tag = TestConstants.EventTags.Random();
                    if (!ev.Tags.Contains(tag))
                        ev.Tags.Add(tag);
                }
            }

            ev.Type = Event.KnownTypes.Error;

            // limit error variation so that stacking will occur
            if (_randomErrors == null)
                _randomErrors = new List<Error>(Enumerable.Range(1, 25).Select(i => GenerateError(maxErrorNestingLevel)));
            
            ev.Data[Event.KnownDataKeys.Error] = _randomErrors.Random();

            return ev;
        }

        private static List<Error> _randomErrors; 

        private static Error GenerateError(int maxErrorNestingLevel = 3, bool generateData = true, int currentNestingLevel = 0) {
            var error = new Error();
            error.Message = @"Generated exception message.";
            error.Type = TestConstants.ExceptionTypes.Random();
            if (RandomHelper.GetBool())
                error.Code = RandomHelper.GetRange(-234523453, 98690899).ToString();

            if (generateData) {
                for (int i = 0; i < RandomHelper.GetRange(1, 5); i++) {
                    string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                    while (error.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                        key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                    error.Data.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
                }
            }

            var stack = new StackFrameCollection();
            for (int i = 0; i < RandomHelper.GetRange(1, 10); i++)
                stack.Add(GenerateStackFrame());
            error.StackTrace = stack;

            if (currentNestingLevel < maxErrorNestingLevel && RandomHelper.GetBool())
                error.Inner = GenerateError(maxErrorNestingLevel, generateData, currentNestingLevel + 1);

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