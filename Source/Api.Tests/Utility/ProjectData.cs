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
    internal static class ProjectData {
        public static IEnumerable<Project> GenerateProjects(int count = 10, bool generateId = false, string id = null, string organizationId = null, Int64? nextSummaryEndOfDayTicks = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateProject(generateId, id, organizationId, nextSummaryEndOfDayTicks: nextSummaryEndOfDayTicks);
        }

        public static List<Project> GenerateSampleProjects() {
            return new List<Project> {
                GenerateSampleProject(),
                GenerateProject(generateId: true, organizationId: TestConstants.OrganizationId2),
                GenerateProject(id: TestConstants.SuspendedProjectId, organizationId: TestConstants.SuspendedOrganizationId)
            };
        }

        public static Project GenerateSampleProject() {
            return GenerateProject(id: TestConstants.ProjectId, name: "Disintegrating Pistol", organizationId: TestConstants.OrganizationId);
        }

        public static Project GenerateProject(bool generateId = false, string id = null, string organizationId = null, string name = null, Int64? nextSummaryEndOfDayTicks = null) {
            var project = new Project {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : String.Empty : id,
                OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
                Name = name ?? String.Format("Project{0}", id)
            };

            if (nextSummaryEndOfDayTicks.HasValue)
                project.NextSummaryEndOfDayTicks = nextSummaryEndOfDayTicks.Value;
            else {
                project.NextSummaryEndOfDayTicks = DateTime.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            }

            for (int i = 0; i < RandomHelper.GetRange(0, 5); i++)
                project.Configuration.Settings.Add(RandomHelper.GetPronouncableString(5), RandomHelper.GetPronouncableString(10));

            return project;
        }
    }
}