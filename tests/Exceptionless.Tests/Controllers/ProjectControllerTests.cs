using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Models;
using FluentRest;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Controllers {
    public sealed class ProjectControllerTests : IntegrationTestsBase {
        public ProjectControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
        }

        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            var service = GetService<SampleDataService>();
            await service.CreateDataAsync();
        }

        [Fact]
        public async Task CanGetProjectConfiguration() {
            var response = await SendRequestAsync(r => r
               .AsFreeOrganizationClientUser()
               .AppendPath("projects/config")
               .StatusCodeShouldBeOk()
            );
            
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
            Assert.True(response.Content.Headers.ContentLength.HasValue);
            Assert.True(response.Content.Headers.ContentLength > 0);

            var config = await response.DeserializeAsync<ClientConfiguration>();
            Assert.True(config.Settings.GetBoolean("IncludeConditionalData"));
            Assert.Equal(0, config.Version);
        }
    }
}
