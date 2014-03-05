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
using MongoDB.Bson;

namespace Exceptionless.Tests.Utility {
    internal static class ErrorData {
        public static IEnumerable<Error> GenerateErrors(int count = 10, bool generateId = false, string id = null, string organizationId = null, string projectId = null, string errorStackId = null, DateTime? startDate = null, DateTime? endDate = null, int minimiumNestingLevel = 0, TimeSpan? timeZoneOffset = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateError(generateId, id, organizationId, projectId, errorStackId, startDate, endDate, timeZoneOffset: timeZoneOffset);
        }

        public static List<Error> GenerateSampleErrors() {
            return new List<Error> {
                GenerateSampleError(),
                GenerateSampleError(TestConstants.ErrorId2),
                GenerateSampleError(TestConstants.ErrorId7),
                GenerateSampleError(TestConstants.ErrorId8),
            };
        }

        public static Error GenerateSampleError(string id = TestConstants.ErrorId) {
            return GenerateError(id: id, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);
        }

        public static Error GenerateError(bool generateId = false, string id = null, string organizationId = null, string projectId = null, string errorStackId = null, DateTime? startDate = null, DateTime? endDate = null, DateTimeOffset? occurrenceDate = null, int nestingLevel = 0, int minimiumNestingLevel = 0, TimeSpan? timeZoneOffset = null) {
            if (!startDate.HasValue)
                startDate = DateTime.Now.AddDays(-90);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            var error = new Error {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : null : id,
                OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
                ProjectId = projectId.IsNullOrEmpty() ? TestConstants.ProjectIds.Random() : projectId,
                RequestInfo = new RequestInfo {
                    ClientIpAddress = RandomHelper.GetIp4Address(),
                    Path = "somepath"
                },
                UserDescription = "Randomly generated test error.",
                OccurrenceDate = occurrenceDate.HasValue ? occurrenceDate.Value : new DateTimeOffset(RandomHelper.GetDateTime(startDate, endDate), timeZoneOffset.HasValue ? timeZoneOffset.Value : TimeZoneInfo.Local.BaseUtcOffset),
                ExceptionlessClientInfo = new ExceptionlessClientInfo {
                    Version = "1.0.0.0"
                },
                EnvironmentInfo = new EnvironmentInfo {
                    MachineName = "Blah"
                }
            };

            if (!errorStackId.IsNullOrEmpty())
                error.ErrorStackId = errorStackId;

            for (int i = 0; i < RandomHelper.GetRange(minimiumNestingLevel, minimiumNestingLevel + 5); i++) {
                string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                while (error.ExtendedData.ContainsKey(key))
                    key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                error.ExtendedData.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
            }

            for (int i = 0; i < RandomHelper.GetRange(minimiumNestingLevel, minimiumNestingLevel + 5); i++) {
                string tag = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                while (error.Tags.Contains(tag))
                    tag = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                error.Tags.Add(tag);
            }

            GenerateErrorInfo(error, nestingLevel, minimiumNestingLevel);

            return error;
        }

        private static ErrorInfo GenerateErrorInfo(int nestingLevel = 0, int minimiumNestingLevel = 0) {
            return GenerateErrorInfo(new ErrorInfo(), nestingLevel, minimiumNestingLevel);
        }

        private static ErrorInfo GenerateErrorInfo(ErrorInfo target, int nestingLevel = 0, int minimiumNestingLevel = 0) {
            target.Message = @"Generated exception message.";
            target.Type = TestConstants.ExceptionTypes.Random();
            if (RandomHelper.GetBool())
                target.Code = RandomHelper.GetRange(-234523453, 98690899).ToString();

            for (int i = 0; i < RandomHelper.GetRange(minimiumNestingLevel, minimiumNestingLevel + 5); i++) {
                string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                while (target.ExtendedData.ContainsKey(key))
                    key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                target.ExtendedData.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
            }

            var stack = new StackFrameCollection();
            for (int i = 0; i < RandomHelper.GetRange(1, 10); i++)
                stack.Add(GenerateStackFrame());
            target.StackTrace = stack;

            if (minimiumNestingLevel > 0 || (nestingLevel < 5 && RandomHelper.GetBool()))
                target.Inner = GenerateErrorInfo(nestingLevel + 1);

            return target;
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