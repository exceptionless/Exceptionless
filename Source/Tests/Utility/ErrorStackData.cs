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
    internal static class EventStackData {
        public static IEnumerable<Stack> GenerateEventStacks(int count = 10, bool generateId = false, string id = null, string organizationId = null, string projectId = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateEventStack(generateId, id, organizationId, projectId);
        }

        public static List<Stack> GenerateSampleEvents() {
            return new List<Stack> {
                GenerateSampleEventStack(),
                GenerateEventStack(id: TestConstants.StackId2, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectIdWithNoRoles),
                GenerateEventStack(id: TestConstants.InvalidStackId)
            };
        }

        public static Stack GenerateSampleEventStack(string id = TestConstants.StackId) {
            return GenerateEventStack(id: id, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId);
        }

        public static Stack GenerateEventStack(bool generateId = false, string id = null, string organizationId = null, string projectId = null) {
            var stack = new Stack {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : null : id,
                OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
                ProjectId = projectId.IsNullOrEmpty() ? TestConstants.ProjectIds.Random() : projectId,
                Title = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 50))
            };

            for (int i = 0; i < RandomHelper.GetRange(0, 5); i++) {
                string tag = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                while (stack.Tags.Contains(tag))
                    tag = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                stack.Tags.Add(tag);
            }

            return stack;
        }
    }
}