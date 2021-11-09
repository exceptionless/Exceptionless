using Exceptionless.Insulation.Configuration;
using Exceptionless.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Exceptionless.Tests;

public class AppWebHostFactory : WebApplicationFactory<Startup> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseSolutionRelativeContentRoot("src/Exceptionless.Web");
    }

    protected override IHostBuilder CreateHostBuilder() {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddYamlFile("appsettings.yml", optional: false, reloadOnChange: false)
            .Build();

        return Program.CreateHostBuilder(config, Environments.Development);
    }
}
