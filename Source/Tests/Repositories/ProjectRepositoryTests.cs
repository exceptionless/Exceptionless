using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class ProjectRepositoryTests {
        public readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        public readonly IProjectRepository _repository = IoC.GetInstance<IProjectRepository>();

        [Fact]
        public async Task IncrementNextSummaryEndOfDayTicksAsync() {
            await _repository.RemoveAllAsync();
            await _client.RefreshAsync();
            Assert.Equal(0, await _repository.CountAsync());

            var project = await _repository.AddAsync(ProjectData.GenerateSampleProject());
            await _client.RefreshAsync();
            Assert.NotNull(project.Id);
            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));
            
            Assert.Equal(1, await _repository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project }));
            await _client.RefreshAsync();

            var updatedProject = await _repository.GetByIdAsync(project.Id);
            // TODO: Modified date isn't currently updated in the update scripts.
            //Assert.NotEqual(project.ModifiedUtc, updatedProject.ModifiedUtc);
            Assert.Equal(project.NextSummaryEndOfDayTicks + TimeSpan.TicksPerDay, updatedProject.NextSummaryEndOfDayTicks);

            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            var project2 = await _repository.AddAsync(ProjectData.GenerateProject(organizationId: project.OrganizationId));
            Assert.NotNull(project2.Id);

            await _client.RefreshAsync();
            Assert.Equal(2, await _repository.CountAsync());
            Assert.Equal(2, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));

            await _repository.RemoveAsync(project2, false);
            await _client.RefreshAsync();
            Assert.Equal(1, await _repository.CountAsync());
            Assert.Equal(1, await _repository.GetCountByOrganizationIdAsync(project.OrganizationId));
        }
    }
}
