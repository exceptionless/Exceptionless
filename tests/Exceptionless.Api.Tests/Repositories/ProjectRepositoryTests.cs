using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Xunit;
using Xunit.Abstractions;
using Foundatio.Repositories;
using Nest;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class ProjectRepositoryTests : ElasticTestBase {
        private readonly ICacheClient _cache;
        private readonly IProjectRepository _repository;

        public ProjectRepositoryTests(ITestOutputHelper output) : base(output) {
            _cache = _configuration.Cache;
            _repository = GetService<IProjectRepository>();
            Log.MinimumLevel = LogLevel.Trace;
        }

        [Fact]
        public async Task IncrementNextSummaryEndOfDayTicksAsync() {
            Assert.Equal(0, await _repository.CountAsync());

            var project = await _repository.AddAsync(ProjectData.GenerateSampleProject());
            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.NotNull(project.Id);
            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            await _repository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project });
            await _configuration.Client.RefreshAsync(Indices.All);

            var updatedProject = await _repository.GetByIdAsync(project.Id);
            // TODO: Modified date isn't currently updated in the update scripts.
            //Assert.NotEqual(project.ModifiedUtc, updatedProject.ModifiedUtc);
            Assert.Equal(project.NextSummaryEndOfDayTicks + TimeSpan.TicksPerDay, updatedProject.NextSummaryEndOfDayTicks);

            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            var project2 = await _repository.AddAsync(ProjectData.GenerateProject(organizationId: project.OrganizationId));
            Assert.NotNull(project2.Id);

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(2, await _repository.CountAsync());
            Assert.Equal(2, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            await _repository.RemoveAsync(project2, o => o.Notifications(false));
            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));
        }

        [Fact]
        public async Task GetByOrganizationIdsAsync() {
            var project1 = await _repository.AddAsync(ProjectData.GenerateProject(id: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, name: "One"), o => o.ImmediateConsistency());
            var project2 = await _repository.AddAsync(ProjectData.GenerateProject(id: TestConstants.SuspendedProjectId, organizationId: TestConstants.OrganizationId, name: "Two"), o => o.ImmediateConsistency());

            Log.SetLogLevel<ProjectRepository>(LogLevel.Trace);
            var results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId, TestConstants.OrganizationId2 });
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);

            results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId });
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);

            results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId });
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);

            await _repository.RemoveAsync(project2.Id, o => o.Notifications(false).ImmediateConsistency());
            results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId });
            Assert.NotNull(results);
            Assert.Equal(1, results.Documents.Count);
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

            var actualCache = await _cache.GetAsync<Project>("Project:" + project.Id);
            Assert.Equal(project.Name, actualCache.Value?.Name);
            var actualCacheToken = actual.GetSlackToken();
            Assert.Equal(token.AccessToken, actualCacheToken?.AccessToken);
        }
    }
}