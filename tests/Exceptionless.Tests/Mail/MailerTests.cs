using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Tests.Utility;
using Foundatio.Logging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Utility;
using RazorSharpEmail;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Mail {
    public class MailerTests : TestBase {
        private readonly IMailer _mailer;

        public MailerTests(ITestOutputHelper output) : base(output) {
            _mailer = GetService<IMailer>();
            if (_mailer is NullMailer)
                _mailer = new Mailer(GetService<IEmailGenerator>(), GetService<IQueue<MailMessage>>(), GetService<FormattingPluginManager>(), GetService<IMetricsClient>(), Log.CreateLogger<Mailer>());
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendLogNotificationAsync() {
            await _mailer.SendEventNoticeAsync(Settings.Current.TestEmailAddress, new EventNotification {
                Event = new PersistentEvent {
                    Id = "1",
                    OrganizationId = "1",
                    ProjectId = "1",
                    StackId = "1",
                    Message = "Happy days are here again...",
                    Type = Event.KnownTypes.Log
                },
                IsNew = true,
                IsCritical = true,
                IsRegression = false,
                TotalOccurrences = 1,
                ProjectName = "Testing"
            });

            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendNotFoundNotificationAsync() {
            await _mailer.SendEventNoticeAsync(Settings.Current.TestEmailAddress, new EventNotification {
                Event = new PersistentEvent {
                    Id = "1",
                    OrganizationId = "1",
                    ProjectId = "1",
                    StackId = "1",
                    Source = "[GET] /not-found?page=20",
                    Type = Event.KnownTypes.NotFound
                },
                IsNew = true,
                IsCritical = true,
                IsRegression = false,
                TotalOccurrences = 1,
                ProjectName = "Testing"
            });

            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendOrganizationHourlyOverageNotificationAsync() {
            await _mailer.SendOrganizationNoticeAsync(Settings.Current.TestEmailAddress, new OrganizationNotificationModel {
                Organization = OrganizationData.GenerateSampleOrganization(),
                IsOverHourlyLimit = true
            });

            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendOrganizationMonthlyOverageNotificationAsync() {
            await _mailer.SendOrganizationNoticeAsync(Settings.Current.TestEmailAddress, new OrganizationNotificationModel {
                Organization = OrganizationData.GenerateSampleOrganization(),
                IsOverMonthlyLimit = true
            });

            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendSimpleErrorNotificationAsync() {
            await _mailer.SendEventNoticeAsync(Settings.Current.TestEmailAddress, new EventNotification {
                Event = new PersistentEvent {
                    Id = "1",
                    OrganizationId = "1",
                    ProjectId = "1",
                    StackId = "1"
                },
                IsNew = true,
                IsCritical = true,
                IsRegression = false,
                TotalOccurrences = 1,
                ProjectName = "Testing"
            });

            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendErrorNotificationAsync() {
            await _mailer.SendEventNoticeAsync(Settings.Current.TestEmailAddress, new EventNotification {
                Event = new PersistentEvent {
                    Id = "1",
                    OrganizationId = "1",
                    ProjectId = "1",
                    StackId = "1"
                },
                IsNew = true,
                IsCritical = true,
                IsRegression = false,
                TotalOccurrences = 1,
                ProjectName = "Testing"
            });

            await RunMailJobAsync();
        }
        
        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendInviteAsync() {
            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            await _mailer.SendInviteAsync(user, organization, new Invite {
                DateAdded = SystemClock.UtcNow,
                EmailAddress = Settings.Current.TestEmailAddress,
                Token = "1"
            });
            
            await RunMailJobAsync();

            var sender = GetService<IMailSender>() as InMemoryMailSender;
            if (sender != null) {
                Assert.Equal(Settings.Current.TestEmailAddress, sender.LastMessage.To);
                Assert.Contains("Join Organization", sender.LastMessage.HtmlBody);
            }
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendAddedToOrganizationAsync() {
            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();

            await _mailer.SendAddedToOrganizationAsync(user, organization, user);
            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendPasswordResetAsync() {
            User user = UserData.GenerateSampleUser();
            await _mailer.SendPasswordResetAsync(user);
            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendVerifyEmailAsync() {
            User user = UserData.GenerateSampleUser();
            await _mailer.SendVerifyEmailAsync(user);
            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendSummaryNotificationAsync() {
            await _mailer.SendDailySummaryAsync(Settings.Current.TestEmailAddress, new DailySummaryModel {
                ProjectId = "1",
                BaseUrl = "http://be.exceptionless.io",
                StartDate = SystemClock.UtcNow.Date,
                EndDate = SystemClock.UtcNow.EndOfDay(),
                NewTotal = 1,
                PerHourAverage = 0.4,
                ProjectName = "Blah",
                Subject = "A daily summary",
                Total = 12,
                UniqueTotal = 1,
                HasSubmittedEvents = true,
                IsFreePlan = false
            });
            await RunMailJobAsync();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendPaymentFailedAsync() {
            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            await _mailer.SendPaymentFailedAsync(user, organization);
            await RunMailJobAsync();
        }

        private async Task RunMailJobAsync() {
            var job = GetService<MailMessageJob>();
            await job.RunAsync();
            
            var sender = GetService<IMailSender>() as InMemoryMailSender;
            if (sender == null)
                return;

            _logger.Info($"To:       {sender.LastMessage.To}");
            _logger.Info($"Subject: {sender.LastMessage.Subject}");
            _logger.Info($"TextBody:\n{sender.LastMessage.TextBody}");
        }
    }
}