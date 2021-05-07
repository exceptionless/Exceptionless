using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Jobs;
using Microsoft.Extensions.Logging;
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
        
        [Fact]
        public async Task CanGetProjectListStats() {
            var projects = await SendRequestAsAsync<List<ViewProject>>(r => r
                .AsTestOrganizationUser()
                .AppendPath("projects")
                .QueryString("mode", "stats")
                .StatusCodeShouldBeOk()
            );

            var project = projects.Single();
            Assert.Equal(0, project.StackCount);
            Assert.Equal(0, project.EventCount);
            
            var (stacks, events) = await CreateDataAsync(d => {
                d.Event().Message("test");
            });
            
            projects = await SendRequestAsAsync<List<ViewProject>>(r => r
                .AsTestOrganizationUser()
                .AppendPath("projects")
                .QueryString("mode", "stats")
                .StatusCodeShouldBeOk()
            );

            project = projects.Single();
            Assert.Equal(stacks.Count, project.StackCount);
            Assert.Equal(events.Count, project.EventCount);
            
            // Reset Project data and ensure soft deleted counts don't show up
            var workItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
                .AsTestOrganizationUser()
                .AppendPath("projects", project.Id, "reset-data")
                .StatusCodeShouldBeAccepted()
            );
            
            Assert.Single(workItems.Workers);
            var workItemJob = GetService<WorkItemJob>();
            await workItemJob.RunUntilEmptyAsync();
            await RefreshDataAsync();
            
            projects = await SendRequestAsAsync<List<ViewProject>>(r => r
                .AsTestOrganizationUser()
                .AppendPath("projects")
                .QueryString("mode", "stats")
                .StatusCodeShouldBeOk()
            );

            project = projects.Single();
            // Stacks and event counts include soft deleted (performance reasons)
            Assert.Equal(stacks.Count, project.StackCount);
            Assert.Equal(events.Count, project.EventCount);

            var cleanupJob = GetService<CleanupDataJob>();
            await cleanupJob.RunAsync();

            projects = await SendRequestAsAsync<List<ViewProject>>(r => r
                .AsTestOrganizationUser()
                .AppendPath("projects")
                .QueryString("mode", "stats")
                .StatusCodeShouldBeOk()
            );

            project = projects.Single();
            Assert.Equal(0, project.StackCount);
            Assert.Equal(0, project.EventCount);
        }
    }
}
