using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Web;
using Foundatio.Xunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ControllerManifestTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public void NoMvcControllersRemain()
    {
        // After the Minimal API migration, no MVC controllers should remain.
        var controllerTypes = typeof(Exceptionless.Web.Program).Assembly.GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        Assert.Empty(controllerTypes);
    }
}
