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
    internal static class StackData {
        public static IEnumerable<Stack> GenerateStacks(int count = 10, bool generateId = false, string id = null, string organizationId = null, string projectId = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateStack(generateId, id, organizationId, projectId);
        }

        public static List<Stack> GenerateSampleStacks() {
            return new List<Stack> {
                GenerateSampleStack(),
                GenerateStack(id: TestConstants.StackId2, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectIdWithNoRoles),
                GenerateStack(id: TestConstants.InvalidStackId)
            };
        }

        public static Stack GenerateSampleStack(string id = TestConstants.StackId) {
            return GenerateStack(id: id, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId);
        }

        public static Stack GenerateStack(bool generateId = false, string id = null, string organizationId = null, string projectId = null, string title = null, string signatureHash = null) {
            var stack = new Stack {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : null : id,
                OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
                ProjectId = projectId.IsNullOrEmpty() ? TestConstants.ProjectIds.Random() : projectId,
                Title = title ?? RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 50)),
                SignatureHash = signatureHash ?? RandomHelper.GetPronouncableString(10),
                SignatureInfo = new SettingsDictionary()
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