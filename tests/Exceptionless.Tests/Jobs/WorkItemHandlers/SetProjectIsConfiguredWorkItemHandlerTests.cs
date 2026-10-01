using Exceptionless.Core;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class SetProjectIsConfiguredWorkItemHandlerTests(ITestOutputHelper output, AppWebHostFactory factory) : IntegrationTestsBase(output, factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleItemAsync_WithConcurrentProjectChanges_PreservesChanges(bool deleteProject)
    {
        // Arrange
        var repository = GetService<IProjectRepository>();
        var project = await repository.AddAsync(GetService<ProjectData>().GenerateProject(generateId: true), o => o.ImmediateConsistency());
        using var interleavingRepository = new InterleavingProjectRepository(
            GetService<ExceptionlessElasticConfiguration>(), GetService<MiniValidationValidator>(), GetService<AppOptions>(),
            () => repository.PatchAsync(project.Id, new PartialPatch(new { name = "Changed during configuration", is_deleted = deleteProject })));
        var handler = new SetProjectIsConfiguredWorkItemHandler(interleavingRepository, GetService<IEventRepository>(), GetService<ILockProvider>(), Log);
        var workItem = new SetProjectIsConfiguredWorkItem { ProjectId = project.Id, IsConfigured = true };
        await using var workItemLock = await handler.GetWorkItemLockAsync(workItem, TestCancellationToken);
        Assert.NotNull(workItemLock);
        var context = new WorkItemContext(workItem, "test-job", workItemLock, TestCancellationToken, static (_, _) => Task.CompletedTask);

        // Act: change/delete the project after the handler reads it, before it writes.
        await handler.HandleItemAsync(context);

        // Assert: first-event configuration must not overwrite edits or resurrect deletions.
        var updated = await repository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes());
        Assert.NotNull(updated);
        Assert.True(updated.IsConfigured);
        Assert.Equal(deleteProject, updated.IsDeleted);
        Assert.Equal("Changed during configuration", updated.Name);
        Assert.Equal(!deleteProject, await repository.GetByIdAsync(project.Id, o => o.Cache()) is not null);
    }

    private sealed class InterleavingProjectRepository(
        ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options, Func<Task<bool>> afterRead)
        : ProjectRepository(configuration, validator, options)
    {
        public override async Task<Project?> GetByIdAsync(Id id, ICommandOptions? options = null)
        {
            var project = await base.GetByIdAsync(id, options);
            Assert.True(await afterRead());
            return project;
        }
    }
}
