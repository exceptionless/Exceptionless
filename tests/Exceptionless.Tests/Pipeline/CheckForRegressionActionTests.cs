using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Pipeline;

public sealed class CheckForRegressionActionTests
{
    [Fact]
    public async Task ProcessBatchAsync_StackWithoutRegression_ContinuesProcessingRemainingStacks()
    {
        // Arrange
        var repository = DispatchProxy.Create<IStackRepository, StackRepositoryProxy>();
        var action = new CheckForRegressionAction(
            repository,
            new SemanticVersionParser(NullLoggerFactory.Instance),
            new AppOptions { DisabledPipelineActions = [] },
            NullLoggerFactory.Instance
        );
        var fixedAt = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        var firstStack = CreateFixedStack("first", fixedAt);
        var secondStack = CreateFixedStack("second", fixedAt);

        var firstContext = CreateContext(firstStack, fixedAt.AddMinutes(-1));
        var secondContext = CreateContext(secondStack, fixedAt.AddMinutes(1));

        // Act
        await action.ProcessBatchAsync([firstContext, secondContext]);

        // Assert
        Assert.False(firstContext.IsRegression);
        Assert.Equal(StackStatus.Fixed, firstStack.Status);
        Assert.True(secondContext.IsRegression);
        Assert.Equal(StackStatus.Regressed, secondStack.Status);
    }

    private static Stack CreateFixedStack(string id, DateTime fixedAt)
    {
        return new Stack
        {
            DateFixed = fixedAt,
            Id = id,
            Status = StackStatus.Fixed
        };
    }

    private static EventContext CreateContext(Stack stack, DateTime eventDate)
    {
        var organization = new Organization { Id = stack.OrganizationId };
        var project = new Project { Id = stack.ProjectId };
        var ev = new PersistentEvent
        {
            Date = new DateTimeOffset(eventDate, TimeSpan.Zero),
            StackId = stack.Id
        };

        return new EventContext(ev, organization, project) { Stack = stack };
    }

    public class StackRepositoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IStackRepository.MarkAsRegressedAsync))
                return Task.CompletedTask;

            throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
        }
    }
}
