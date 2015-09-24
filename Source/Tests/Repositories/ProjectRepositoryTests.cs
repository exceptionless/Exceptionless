using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class ProjectRepositoryTests {
        public readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        public readonly IProjectRepository _repository = IoC.GetInstance<IProjectRepository>();

        [Fact]
        public async Task IncrementNextSummaryEndOfDayTicks() {
            await _repository.RemoveAllAsync();
            Assert.Equal(0, await _repository.CountAsync());
            
            var project = await _repository.AddAsync(ProjectData.GenerateSampleProject());
            Assert.NotNull(project.Id);

            await _client.RefreshAsync();
            Assert.Equal(1, await _repository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project.Id }));
            await _client.RefreshAsync();

            var updatedProject = await _repository.GetByIdAsync(project.Id, false);
            Assert.Equal(project.NextSummaryEndOfDayTicks + TimeSpan.TicksPerDay, updatedProject.NextSummaryEndOfDayTicks);

            //TODO: Figure out why this isn't updated.
            //Assert.NotEqual(project.ModifiedUtc, updatedProject.ModifiedUtc);
        }
    }
}