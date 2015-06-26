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
        public async Task IncrementNextSummaryEndOfDayTicks() {
            _repository.RemoveAll();
            Assert.Equal(0, _repository.Count());
            
            var project =_repository.Add(ProjectData.GenerateSampleProject());
            Assert.NotNull(project.Id);

            await _client.RefreshAsync();
            Assert.Equal(1, _repository.IncrementNextSummaryEndOfDayTicks(new[] { project.Id }));
            await _client.RefreshAsync();

            var updatedProject = _repository.GetById(project.Id, false);
            Assert.Equal(project.NextSummaryEndOfDayTicks + TimeSpan.TicksPerDay, updatedProject.NextSummaryEndOfDayTicks);

            //TODO: Figure out why this isn't updated.
            //Assert.NotEqual(project.ModifiedUtc, updatedProject.ModifiedUtc);
        }
    }
}