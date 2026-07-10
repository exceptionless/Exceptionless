using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ControllerRemovalTests
{
    [Fact]
    public void ControllerTypes_AfterMinimalApiMigration_IsEmpty()
    {
        // Arrange
        var webAssembly = typeof(Exceptionless.Web.Program).Assembly;

        // Act
        var controllerTypes = webAssembly.GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        // Assert
        Assert.Empty(controllerTypes);
    }
}
