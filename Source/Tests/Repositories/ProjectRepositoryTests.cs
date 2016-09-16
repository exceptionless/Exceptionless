using System;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Xunit;
using Xunit.Abstractions;
using Foundatio.Logging;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class ProjectRepositoryTests : ElasticTestBase {
        private readonly InMemoryCacheClient _cache;
        private readonly IProjectRepository _repository;

        public ProjectRepositoryTests(ITestOutputHelper output) : base(output) {
            _cache = _configuration.Cache as InMemoryCacheClient;
            _repository = GetService<IProjectRepository>();
            Log.MinimumLevel = LogLevel.Trace;
        }

        [Fact]
        public async Task IncrementNextSummaryEndOfDayTicksAsync() {
            Assert.Equal(0, await _repository.CountAsync());

            var project = await _repository.AddAsync(ProjectData.GenerateSampleProject());
            await _configuration.Client.RefreshAsync();
            Assert.NotNull(project.Id);
            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));
            
            Assert.Equal(1, await _repository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project }));
            await _configuration.Client.RefreshAsync();

            var updatedProject = await _repository.GetByIdAsync(project.Id);
            // TODO: Modified date isn't currently updated in the update scripts.
            //Assert.NotEqual(project.ModifiedUtc, updatedProject.ModifiedUtc);
            Assert.Equal(project.NextSummaryEndOfDayTicks + TimeSpan.TicksPerDay, updatedProject.NextSummaryEndOfDayTicks);

            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            var project2 = await _repository.AddAsync(ProjectData.GenerateProject(organizationId: project.OrganizationId));
            Assert.NotNull(project2.Id);

            await _configuration.Client.RefreshAsync();
            Assert.Equal(2, await _repository.CountAsync());
            Assert.Equal(2, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            await _repository.RemoveAsync(project2, false);
            await _configuration.Client.RefreshAsync();
            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));
        }

        [Fact]
        public async Task GetByOrganizationIdsAsync() {
            var project1 = await _repository.AddAsync(ProjectData.GenerateProject(id: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, name: "One"));
            var project2 = await _repository.AddAsync(ProjectData.GenerateProject(id: TestConstants.SuspendedProjectId, organizationId: TestConstants.OrganizationId, name: "Two"));

            Assert.Equal(0, _cache.Count);
            Assert.Equal(0, _cache.Hits);
            Assert.Equal(0, _cache.Misses);

            Log.SetLogLevel<ProjectRepository>(LogLevel.Trace);
            await _configuration.Client.RefreshAsync();
            var results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId, TestConstants.OrganizationId2 });
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);
            Assert.Equal(0, _cache.Count);
            Assert.Equal(0, _cache.Hits);
            Assert.Equal(0, _cache.Misses);

            results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId });
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);
            Assert.Equal(1, _cache.Count);
            Assert.Equal(0, _cache.Hits);
            Assert.Equal(1, _cache.Misses);

            results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId });
            Assert.NotNull(results);
            Assert.Equal(2, results.Documents.Count);
            Assert.Equal(1, _cache.Count);
            Assert.Equal(1, _cache.Hits);
            Assert.Equal(1, _cache.Misses);

            await _repository.RemoveAsync(project2.Id, false);
            Assert.Equal(0, _cache.Count);
            Assert.Equal(1, _cache.Hits);
            Assert.Equal(2, _cache.Misses);

            await _configuration.Client.RefreshAsync();
            results = await _repository.GetByOrganizationIdsAsync(new[] { project1.OrganizationId });
            Assert.NotNull(results);
            Assert.Equal(1, results.Documents.Count);
            Assert.Equal(1, _cache.Count);
            Assert.Equal(1, _cache.Hits);
            Assert.Equal(3, _cache.Misses);
            
            await _repository.RemoveAllAsync(false);
            Assert.Equal(0, _cache.Count);
            Assert.Equal(1, _cache.Hits);
            Assert.Equal(3, _cache.Misses);
        }
    }
}