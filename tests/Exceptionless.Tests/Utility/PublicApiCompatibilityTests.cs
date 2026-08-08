using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Utility;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class PublicApiCompatibilityTests
{
    [Fact]
    public void IExtensibleObject_GetProperty_PreservesOriginalSignature()
    {
        var method = typeof(IExtensibleObject)
            .GetMethods()
            .Single(method => method.Name == nameof(IExtensibleObject.GetProperty) && method.IsGenericMethodDefinition);

        Assert.NotNull(method);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal("name", parameter.Name);
        Assert.Equal(typeof(string), parameter.ParameterType);
    }

    [Fact]
    public void ProjectExtensions_GetSlackToken_PreservesSerializerlessOverload()
    {
        var project = new Project
        {
            Data = new DataDictionary
            {
                [Project.KnownDataKeys.SlackToken] = new SlackToken { AccessToken = "xoxb-test" }
            }
        };

        SlackToken? token = project.GetSlackToken();

        Assert.NotNull(token);
        Assert.Equal("xoxb-test", token.AccessToken);
    }

    [Fact]
    public void DataDictionaryExtensions_GetValue_PreservesJsonSerializerOptionsOverload()
    {
        var data = new DataDictionary
        {
            [Event.KnownDataKeys.EnvironmentInfo] = new Dictionary<string, object?>
            {
                ["MachineName"] = "compat-node",
                ["OSName"] = "Linux"
            }
        };
        var options = new JsonSerializerOptions().ConfigureExceptionlessDefaults();

        var environment = data.GetValue<EnvironmentInfo>(Event.KnownDataKeys.EnvironmentInfo, options);

        Assert.NotNull(environment);
        Assert.Equal("compat-node", environment.MachineName);
        Assert.Equal("Linux", environment.OSName);
    }

    [Fact]
    public void Stack_InternalCleanupState_IsNotSerializedByApi()
    {
        var stack = new Stack
        {
            RedirectToStackId = "target-stack",
            MergedDuplicateStackTotals = new Dictionary<string, int> { ["source-stack"] = 42 },
            NeedsRedirectReconciliation = true
        };

        string apiJson = JsonSerializer.Serialize(stack, new JsonSerializerOptions().ConfigureExceptionlessApiDefaults());
        string storageJson = JsonSerializer.Serialize(stack, new JsonSerializerOptions().ConfigureExceptionlessDefaults());

        Assert.DoesNotContain("redirect_to_stack_id", apiJson);
        Assert.DoesNotContain("merged_duplicate_stack_totals", apiJson);
        Assert.DoesNotContain("needs_redirect_reconciliation", apiJson);
        Assert.Contains("redirect_to_stack_id", storageJson);
        Assert.Contains("merged_duplicate_stack_totals", storageJson);
        Assert.Contains("needs_redirect_reconciliation", storageJson);
    }
}
