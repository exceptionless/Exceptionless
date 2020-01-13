using System;
using System.Threading.Tasks;
using Exceptionless.Tests.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Controllers {
    public class StatusControllerTests : IntegrationTestsBase {
        public StatusControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
        }

        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            
            var service = GetService<SampleDataService>();
            await service.CreateDataAsync();
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(null, true)]
        //[InlineData(null, true, false)] // TODO: Resolve issue where you are required to pass a message via the body.
        [InlineData("New Release!!", false)]
        [InlineData("New Release!!", true)]
        public async Task CanSendReleaseNotification(string message, bool critical, bool sendMessageAsContentIfEmpty = true) {
            Log.MinimumLevel = LogLevel.Trace;
            var utcNow = SystemClock.UtcNow;

            ReleaseNotification notification;
            if (!String.IsNullOrEmpty(message) || sendMessageAsContentIfEmpty) {
                notification = await SendRequestAsAsync<ReleaseNotification>(r => r
                    .Post()
                    .AsGlobalAdminUser()
                    .AppendPath("notifications/release")
                    .QueryStringIf(() => critical, "critical", critical)
                    .Content(new ValueFromBody<string>(message))
                    .StatusCodeShouldBeOk());
            } else {
                notification = await SendRequestAsAsync<ReleaseNotification>(r => r
                    .Post()
                    .AsGlobalAdminUser()
                    .AppendPath("notifications/release")
                    .QueryStringIf(() => critical, "critical", critical)
                    .StatusCodeShouldBeOk());
            }

            Assert.Equal(message, notification.Message);
            Assert.Equal(critical, notification.Critical);
            Assert.True(notification.Date.IsAfterOrEqual(utcNow));
        }
    }
}
