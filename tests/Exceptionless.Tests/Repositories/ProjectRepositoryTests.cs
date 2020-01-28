using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Xunit;
using Xunit.Abstractions;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Tests.Repositories {
    public sealed class ProjectRepositoryTests : IntegrationTestsBase {
        private readonly ICacheClient _cache;
        private readonly IProjectRepository _repository;

        public ProjectRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _cache = GetService<ICacheClient>();
            _repository = GetService<IProjectRepository>();
        }

        [Fact]
        public async Task IncrementNextSummaryEndOfDayTicksAsync() {
            Assert.Equal(0, await _repository.CountAsync());

            var project = await _repository.AddAsync(ProjectData.GenerateSampleProject(), o => o.ImmediateConsistency());
            Assert.NotNull(project.Id);
            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            await _repository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project });
            await RefreshDataAsync();

            var updatedProject = await _repository.GetByIdAsync(project.Id);
            // TODO: Modified date isn't currently updated in the update scripts.
            //Assert.NotEqual(project.ModifiedUtc, updatedProject.ModifiedUtc);
            Assert.Equal(project.NextSummaryEndOfDayTicks + TimeSpan.TicksPerDay, updatedProject.NextSummaryEndOfDayTicks);

            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            var project2 = await _repository.AddAsync(ProjectData.GenerateProject(organizationId: project.OrganizationId), o => o.ImmediateConsistency());
            Assert.NotNull(project2.Id);

            Assert.Equal(2, await _repository.CountAsync());
            Assert.Equal(2, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            await _repository.RemoveAsync(project2, o => o.Notifications(false).ImmediateConsistency());
            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));
        }

        [Fact]
        public async Task GetByOrganizationIdsAsync() {
            var project1 = await _repository.AddAsync(ProjectData.GenerateProject(id: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, name: "One"), o => o.ImmediateConsistency());
            var project2 = await _repository.AddAsync(ProjectData.GenerateProject(id: TestConstants.SuspendedProjectId, organizationId: TestConstants.OrganizationId, name: "Two"), o => o.ImmediateConsistency());

            var results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId, TestConstants.OrganizationId2 });
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);

            results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId });
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);

            results = await _repository.GetByOrganizationIdsAsync(new[] { TestConstants.OrganizationId2 });
            Assert.NotNull(results);
            Assert.Empty(results.Documents);

            await _repository.RemoveAsync(project2.Id, o => o.Notifications(false).ImmediateConsistency());
            results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId });
            Assert.NotNull(results);
            Assert.Single(results.Documents);
            await _repository.RemoveAllAsync(o => o.Notifications(false));
        }

        [Fact]
        public async Task GetByFilterAsyncAsync() {
            var organizations = OrganizationData.GenerateSampleOrganizations(GetService<BillingManager>(), GetService<BillingPlans>());
            var organization1 = organizations.Single(o => String.Equals(o.Id, TestConstants.OrganizationId));
            var organization2 = organizations.Single(o => String.Equals(o.Id, TestConstants.OrganizationId2));
            
            var project1 = await _repository.AddAsync(ProjectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id, name: "One"), o => o.ImmediateConsistency());
            var project2 = await _repository.AddAsync(ProjectData.GenerateProject(id: TestConstants.SuspendedProjectId, organizationId: organization1.Id, name: "Two"), o => o.ImmediateConsistency());

            var results = await _repository.GetByFilterAsync(new AppFilter(organizations), null, null);
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);

            results = await _repository.GetByFilterAsync(new AppFilter(organization1), null, null);
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);

            results = await _repository.GetByFilterAsync(new AppFilter(organization2), null, null);
            Assert.NotNull(results);
            Assert.Empty(results.Documents);
            
            results = await _repository.GetByFilterAsync(new AppFilter(organization1), "name:one", null);
            Assert.NotNull(results);
            Assert.Single(results.Documents);
            Assert.Equal(project1.Name, results.Documents.Single().Name);

            await _repository.RemoveAsync(project2.Id, o => o.Notifications(false).ImmediateConsistency());
            results = await _repository.GetByFilterAsync(new AppFilter(organization1), null, null);
            Assert.NotNull(results);
            Assert.Single(results.Documents);
            await _repository.RemoveAllAsync(o => o.Notifications(false));
        }
        
        [Fact]
        public async Task CanRoundTripWithCaching() {
            var token = new SlackToken {
                AccessToken = "MY KEY",
                IncomingWebhook = new SlackToken.IncomingWebHook {
                    Url = "MY Url"
                }
            };

            var project = ProjectData.GenerateSampleProject();
            project.Data[Project.KnownDataKeys.SlackToken] = token;

            await _repository.AddAsync(project, o => o.ImmediateConsistency());
            var actual = await _repository.GetByIdAsync(project.Id, o => o.Cache());
            Assert.Equal(project.Name, actual?.Name);
            var actualToken = actual.GetSlackToken();
            Assert.Equal(token.AccessToken, actualToken?.AccessToken);

            var actualCache = await _cache.GetAsync<ICollection<FindHit<Project>>>("Project:" + project.Id);
            Assert.True(actualCache.HasValue);
            Assert.Equal(project.Name, actualCache.Value.Single().Document.Name);
            var actualCacheToken = actual.GetSlackToken();
            Assert.Equal(token.AccessToken, actualCacheToken?.AccessToken);
        }
    }
}