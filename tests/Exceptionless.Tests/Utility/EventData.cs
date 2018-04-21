using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Utility;

namespace Exceptionless.Tests.Utility {
    internal static class EventData {
        public static IEnumerable<PersistentEvent> GenerateEvents(int count = 10, string[] organizationIds = null, string[] projectIds = null, string[] stackIds = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, int maxErrorNestingLevel = 3, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false, string[] referenceIds = null, decimal? value = -1, string semver = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateEvent(organizationIds, projectIds, stackIds, startDate, endDate, generateTags: generateTags, generateData: generateData, isFixed: isFixed, isHidden: isHidden, maxErrorNestingLevel: maxErrorNestingLevel, referenceIds: referenceIds, value: value, semver: semver);
        }

        public static IEnumerable<PersistentEvent> GenerateEvents(int count = 10, string organizationId = null, string projectId = null, string stackId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, int maxErrorNestingLevel = 3, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false, string referenceId = null, decimal? value = -1, string semver = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateEvent(organizationId, projectId, stackId, startDate, endDate, generateTags: generateTags, generateData: generateData, isFixed: isFixed, isHidden: isHidden, maxErrorNestingLevel: maxErrorNestingLevel, referenceId: referenceId, value: value, semver: semver);
        }

        public static PersistentEvent GenerateSampleEvent() {
            return GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, maxErrorNestingLevel: 4);
        }

        public static PersistentEvent GenerateEvent(string organizationId = null, string projectId = null, string stackId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, DateTimeOffset? occurrenceDate = null, int maxErrorNestingLevel = 0, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false, string referenceId = null, string type = null, string sessionId = null, string userIdentity = null, decimal? value = -1, string semver = null) {
            return GenerateEvent(
                    organizationId != null ? new[] { organizationId } : null,
                    projectId != null ? new[] { projectId } : null,
                    stackId != null ? new[] { stackId } : null,
                    startDate, endDate, occurrenceDate, maxErrorNestingLevel, generateTags, generateData, isFixed, isHidden,
                    referenceId != null ? new[] { referenceId } : null,
                    type,
                    sessionId,
                    userIdentity,
                    value,
                    semver
                );
        }

        public static PersistentEvent GenerateSessionStartEvent(DateTimeOffset occurrenceDate, string sessionId = null, string userIdentity = null, decimal? value = -1) {
            return GenerateEvent(projectIds: new string[0], type: Event.KnownTypes.Session, occurrenceDate: occurrenceDate, sessionId: sessionId, userIdentity: userIdentity, generateData: false, generateTags: false, value: value);
        }

        public static PersistentEvent GenerateSessionEndEvent(DateTimeOffset occurrenceDate, string sessionId = null, string userIdentity = null) {
            return GenerateEvent(projectIds: new string[0], type: Event.KnownTypes.SessionEnd, occurrenceDate: occurrenceDate, sessionId: sessionId, userIdentity: userIdentity, generateData: false, generateTags: false);
        }

        public static PersistentEvent GenerateEvent(string[] organizationIds = null, string[] projectIds = null, string[] stackIds = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, DateTimeOffset? occurrenceDate = null, int maxErrorNestingLevel = 0, bool generateTags = true, bool generateData = true, bool isFixed = false, bool isHidden = false, string[] referenceIds = null, string type = null, string sessionId = null,  string userIdentity = null, decimal? value = -1, string semver = null) {
            if (!startDate.HasValue || startDate > SystemClock.OffsetNow.AddHours(1))
                startDate = SystemClock.OffsetNow.AddDays(-30);
            if (!endDate.HasValue || endDate > SystemClock.OffsetNow.AddHours(1))
                endDate = SystemClock.OffsetNow;

            var ev = new PersistentEvent {
                OrganizationId = organizationIds.Random(TestConstants.OrganizationId),
                ProjectId = projectIds.Random(TestConstants.ProjectId),
                ReferenceId = referenceIds.Random(),
                Date = occurrenceDate ?? RandomData.GetDateTimeOffset(startDate, endDate),
                Value = value.GetValueOrDefault() >= 0 ? value : RandomData.GetDecimal(0, Int32.MaxValue),
                IsFixed = isFixed,
                IsHidden = isHidden,
                StackId = stackIds.Random()
            };

            if (!String.IsNullOrEmpty(userIdentity))
                ev.SetUserIdentity(userIdentity);

            if (generateData) {
                for (int i = 0; i < RandomData.GetInt(1, 5); i++) {
                    string key = RandomData.GetWord();
                    while (ev.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                        key = RandomData.GetWord();

                    ev.Data.Add(key, RandomData.GetWord());
                }
            }

            if (generateTags) {
                for (int i = 0; i < RandomData.GetInt(1, 3); i++) {
                    string tag = TestConstants.EventTags.Random();
                    if (!ev.Tags.Contains(tag))
                        ev.Tags.Add(tag);
                }
            }

            if (String.IsNullOrEmpty(type) || String.Equals(type, Event.KnownTypes.Error, StringComparison.OrdinalIgnoreCase)) {
                ev.Type = Event.KnownTypes.Error;

                // limit error variation so that stacking will occur
                if (_randomErrors == null)
                    _randomErrors = new List<Error>(Enumerable.Range(1, 25).Select(i => GenerateError(maxErrorNestingLevel)));

                ev.Data[Event.KnownDataKeys.Error] = _randomErrors.Random();
            } else {
                ev.Type = type.ToLowerInvariant();
            }

            if (!String.IsNullOrEmpty(sessionId))
                ev.SetSessionId(sessionId);

            if (ev.IsSessionStart())
                ev.Value = null;

            ev.SetVersion(semver);
            return ev;
        }

        private static List<Error> _randomErrors;

        internal static Error GenerateError(int maxErrorNestingLevel = 3, bool generateData = true, int currentNestingLevel = 0) {
            var error = new Error {
                Message = "Generated exception message.",
                Type = TestConstants.ExceptionTypes.Random()
            };

            if (RandomData.GetBool())
                error.Code = RandomData.GetInt(-234523453, 98690899).ToString();

            if (generateData) {
                for (int i = 0; i < RandomData.GetInt(1, 5); i++) {
                    string key = RandomData.GetWord();
                    while (error.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                        key = RandomData.GetWord();

                    error.Data.Add(key, RandomData.GetWord());
                }
            }

            var stack = new StackFrameCollection();
            for (int i = 0; i < RandomData.GetInt(1, 10); i++)
                stack.Add(GenerateStackFrame());
            error.StackTrace = stack;

            if (currentNestingLevel < maxErrorNestingLevel && RandomData.GetBool())
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