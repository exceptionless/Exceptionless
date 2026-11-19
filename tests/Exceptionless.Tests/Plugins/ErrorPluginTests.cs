using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class ErrorPluginTests : TestWithServices
{
    private readonly ErrorPlugin _plugin;

    public ErrorPluginTests(ITestOutputHelper output) : base(output)
    {
        _plugin = GetService<ErrorPlugin>();
    }

    [Fact]
    public async Task EventProcessingAsync_NoUserNamespaces_UsesFirstNonSystemFrame()
    {
        // Arrange
        var context = CreateContext();

        // Act
        await _plugin.EventProcessingAsync(context);

        // Assert
        Assert.Contains("Integer.parseInt", context.StackSignatureData["Method"]);
    }

    [Theory]
    [InlineData("UserNamespaces", "com.example", true)]
    [InlineData("CommonMethods", "Integer.parseInt", true)]
    [InlineData("UserNamespaces", "com.example", false)]
    public async Task EventProcessingAsync_StackingSetting_UsesUserFrame(string key, string value, bool inProjectConfiguration)
    {
        // Arrange
        var context = CreateContext();
        if (inProjectConfiguration)
            context.Project.Configuration.Settings[key] = value;
        else
            context.SetProperty(key, value);

        // Act
        await _plugin.EventProcessingAsync(context);

        // Assert
        Assert.Contains("Foo.parse", context.StackSignatureData["Method"]);
    }

    private static EventContext CreateContext()
    {
        var ev = new PersistentEvent
        {
            Type = Event.KnownTypes.Error,
            Data = new DataDictionary
            {
                [Event.KnownDataKeys.Error] = new Error
                {
                    Type = "java.lang.NumberFormatException",
                    Message = "For input string: \"abc\"",
                    StackTrace =
                    [
                        new StackFrame
                        {
                            DeclaringNamespace = "java.lang",
                            DeclaringType = "Integer",
                            Name = "parseInt"
                        },
                        new StackFrame
                        {
                            DeclaringNamespace = "com.example",
                            DeclaringType = "Foo",
                            Name = "parse"
                        }
                    ]
                }
            }
        };

        return new EventContext(ev, new Organization { Id = TestConstants.OrganizationId },
            new Project { Id = TestConstants.ProjectId, OrganizationId = TestConstants.OrganizationId });
    }
}
