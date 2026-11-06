using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class StackStatusJobTests : IntegrationTestsBase
{
    private readonly StackStatusJob _job;
    private readonly StackData _stackData;
    private readonly IStackRepository _stackRepository;

    public StackStatusJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<StackStatusJob>();
        _stackData = GetService<StackData>();
        _stackRepository = GetService<IStackRepository>();
    }

    [Fact]
    public async Task RunAsync_WithMoreThanOnePageOfExpiredSnoozedStacks_OpensEveryStack()
    {
        // Arrange
        DateTime utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var stacks = _stackData.GenerateStacks(
                201,
                generateId: true,
                organizationId: TestConstants.OrganizationId,
                projectId: TestConstants.ProjectId)
            .ToList();
        foreach (var stack in stacks)
        {
            stack.Status = StackStatus.Snoozed;
            stack.SnoozeUntilUtc = utcNow.AddMinutes(-1);
            stack.DateFixed = utcNow.AddDays(-1);
            stack.FixedInVersion = "1.0.0";
        }

        await _stackRepository.AddAsync(stacks, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var updatedStacks = await _stackRepository.GetByIdsAsync(
            stacks.Select(stack => stack.Id).ToArray(),
            o => o.ImmediateConsistency());
        Assert.Equal(201, updatedStacks.Count);
        Assert.All(updatedStacks, stack =>
        {
            Assert.Equal(StackStatus.Open, stack.Status);
            Assert.Null(stack.SnoozeUntilUtc);
            Assert.Null(stack.DateFixed);
            Assert.Null(stack.FixedInVersion);
        });
    }
}
