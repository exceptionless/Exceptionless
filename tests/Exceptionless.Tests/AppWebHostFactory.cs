
using System;
using Exceptionless.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Exceptionless.Tests {
    public class AppWebHostFactory : WebApplicationFactory<Startup> {
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            builder.ConfigureServices(services => {
                builder.UseSolutionRelativeContentRoot("src/Exceptionless.Web");
            });
        }
    }
}
