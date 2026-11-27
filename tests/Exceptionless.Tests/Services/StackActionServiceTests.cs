using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class StackActionServiceTests : IntegrationTestsBase
{
    private readonly StackActionService _service;
    private readonly IStackRepository _repository;
    private readonly StackData _stackData;

    public StackActionServiceTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _service = GetService<StackActionService>();
        _repository = GetService<IStackRepository>();
        _stackData = GetService<StackData>();
    }

    [Fact]
    public async Task MarkFixedAsync_WhenOccurrenceIsCountedAfterTheStackWasRead_KeepsTheCounter()
    {
        // Arrange
        var stack = await _repository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, status: StackStatus.Open, totalOccurrences: 1), o => o.ImmediateConsistency());
        var stale = await _repository.GetByIdAsync(stack.Id, o => o.Cache(false));
        Assert.NotNull(stale);

        await _repository.IncrementEventCounterAsync(stack.OrganizationId, stack.ProjectId, stack.Id, TimeProvider.GetUtcNow().UtcDateTime, TimeProvider.GetUtcNow().UtcDateTime, 1);
        await RefreshDataAsync();

        // Act
        await _service.MarkFixedAsync([stale], null);
        await RefreshDataAsync();

        // Assert
        var actual = await _repository.GetByIdAsync(stack.Id, o => o.Cache(false));
        Assert.NotNull(actual);
        Assert.Equal(StackStatus.Fixed, actual.Status);
        Assert.NotNull(actual.DateFixed);
        Assert.Equal(2, actual.TotalOccurrences);
    }

    [Fact]
    public async Task SnoozeAsync_WhenFixedStack_ClearsTheFixAndKeepsTheCounter()
    {
        // Arrange
        var fixedStack = _stackData.GenerateStack(id: TestConstants.StackId, status: StackStatus.Fixed, totalOccurrences: 1);
        fixedStack.DateFixed = TimeProvider.GetUtcNow().UtcDateTime;
        fixedStack.FixedInVersion = "1.0.0";
        await _repository.AddAsync(fixedStack, o => o.ImmediateConsistency());
        var stale = await _repository.GetByIdAsync(fixedStack.Id, o => o.Cache(false));
        Assert.NotNull(stale);

        await _repository.IncrementEventCounterAsync(fixedStack.OrganizationId, fixedStack.ProjectId, fixedStack.Id, TimeProvider.GetUtcNow().UtcDateTime, TimeProvider.GetUtcNow().UtcDateTime, 1);
        var until = TimeProvider.GetUtcNow().UtcDateTime.AddHours(6);

        // Act
        await _service.SnoozeAsync([stale], until);
        await RefreshDataAsync();

        // Assert
        var actual = await _repository.GetByIdAsync(fixedStack.Id, o => o.Cache(false));
        Assert.NotNull(actual);
        Assert.Equal(StackStatus.Snoozed, actual.Status);
        Assert.Equal(until, actual.SnoozeUntilUtc);
        Assert.Null(actual.FixedInVersion);
        Assert.Null(actual.DateFixed);
        Assert.Equal(2, actual.TotalOccurrences);
    }

    [Fact]
    public async Task DeleteAsync_WhenOccurrenceIsCountedAfterTheStackWasRead_KeepsTheCounter()
    {
        // Arrange
        var stack = await _repository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, totalOccurrences: 1), o => o.ImmediateConsistency());
        var stale = await _repository.GetByIdAsync(stack.Id, o => o.Cache(false));
        Assert.NotNull(stale);

        await _repository.IncrementEventCounterAsync(stack.OrganizationId, stack.ProjectId, stack.Id, TimeProvider.GetUtcNow().UtcDateTime, TimeProvider.GetUtcNow().UtcDateTime, 1);

        // Act
        await _service.DeleteAsync([stale]);
        await RefreshDataAsync();

        // Assert
        Assert.Null(await _repository.GetByIdAsync(stack.Id, o => o.Cache(false)));
        var actual = await _repository.GetByIdAsync(stack.Id, o => o.Cache(false).IncludeSoftDeletes());
        Assert.NotNull(actual);
        Assert.True(actual.IsDeleted);
        Assert.Equal(2, actual.TotalOccurrences);
    }
}
