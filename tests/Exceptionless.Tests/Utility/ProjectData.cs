using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;

namespace Exceptionless.Tests.Utility {
    internal static class ProjectData {
        public static IEnumerable<Project> GenerateProjects(int count = 10, bool generateId = false, string id = null, string organizationId = null, long? nextSummaryEndOfDayTicks = null) {
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

        public static Project GenerateProject(bool generateId = false, string id = null, string organizationId = null, string name = null, long? nextSummaryEndOfDayTicks = null) {
            var project = new Project {
                Id = !id.IsNullOrEmpty() ? id : generateId ? ObjectId.GenerateNewId().ToString() : null,
                OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
                Name = name ?? $"Project{id}"
            };

            if (nextSummaryEndOfDayTicks.HasValue)
                project.NextSummaryEndOfDayTicks = nextSummaryEndOfDayTicks.Value;
            else {
                project.NextSummaryEndOfDayTicks = SystemClock.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            }

            for (int i = 0; i < RandomData.GetInt(0, 5); i++) {
                string key = RandomData.GetWord();
                while (project.Configuration.Settings.ContainsKey(key))
                    key = RandomData.GetWord();

                project.Configuration.Settings.Add(key, RandomData.GetWord());
            }

            return project;
        }
    }
}