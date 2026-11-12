using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using Aspire.Hosting.Testing;
using Foundatio.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests;

public sealed class AppHostTopologyTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuildAsync_ClientPackageInstallation_RunsOnlyWhenNotAlreadyInstalledByCi(bool ciE2E)
    {
        // Arrange
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Exceptionless_AppHost>(
            ciE2E ? ["--ci-e2e"] : [], TestCancellationToken);

        // Act
        await using var application = await builder.BuildAsync(TestCancellationToken);
        var model = application.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var installer = Assert.Single(model.Resources.OfType<JavaScriptInstallerResource>(), resource => resource.Name == "App-installer");
        var app = Assert.Single(model.Resources, resource => resource.Name == "App");
        Assert.Equal(ciE2E, installer.Annotations.OfType<ExplicitStartupAnnotation>().Any());
        Assert.Equal(!ciE2E, app.Annotations.OfType<WaitAnnotation>().Any(wait => wait.Resource == installer));
        Assert.Contains(app.Annotations.OfType<WaitAnnotation>(), wait => wait.Resource.Name == "Api");
    }
}
