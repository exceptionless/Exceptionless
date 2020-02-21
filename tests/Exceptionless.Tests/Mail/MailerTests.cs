using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Mail {
    public sealed class MailerTests : TestWithServices {
        private readonly IMailer _mailer;
        private readonly AppOptions _options;
        private readonly BillingManager _billingManager;
        private readonly BillingPlans _plans;

        public MailerTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {
            _mailer = GetService<IMailer>();
            _options = GetService<AppOptions>();
            _billingManager = GetService<BillingManager>();
            _plans = GetService<BillingPlans>();

            if (_mailer is NullMailer)
                _mailer = new Mailer(GetService<IQueue<MailMessage>>(), GetService<FormattingPluginManager>(), _options, GetService<IMetricsClient>(), Log.CreateLogger<Mailer>());
        }


        [Fact]
        public void CanParseSmtpUri() {
            var uri = new SmtpUri("smtps://test%40test.com:testpass@smtp.test.com:587");
            Assert.NotNull(uri);
            Assert.True(uri.IsSecure);
            Assert.Equal("smtp.test.com", uri.Host);
            Assert.Equal(587, uri.Port);
            Assert.Equal("test@test.com", uri.User);
            Assert.Equal("testpass", uri.Password);
        }
        
        [Fact]
        public Task SendEventNoticeSimpleErrorAsync() {
            var ex = GetException();
            return SendEventNoticeAsync(new PersistentEvent {
                Type = Event.KnownTypes.Error,
                Data = new Core.Models.DataDictionary {
                    {
                        Event.KnownDataKeys.SimpleError, new SimpleError {
                            Message = ex.Message,
                            Type = ex.GetType().FullName,
                            StackTrace = ex.StackTrace
                        }
                    }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeErrorAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Type = Event.KnownTypes.Error,
                Data = new Core.Models.DataDictionary {
                    {
                        Event.KnownDataKeys.Error, EventData.GenerateError()
                    }
                }
            });
        }


        [Fact]
        public Task SendEventNoticeErrorWithDetailsAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Type = Event.KnownTypes.Error,
                Geo = "44.5241,-87.9056",
                ReferenceId = "ex_blake_dreams_of_cookies",
                Tags = new TagSet(new [] { "Out", "Of", "Cookies", "Critical" }),
                Count = 2,
                Value = 500,
                Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Error, EventData.GenerateError() },
                    { Event.KnownDataKeys.Version, "1.2.3" },
                    { Event.KnownDataKeys.UserInfo, new UserInfo("niemyjski", "Blake Niemyjski")  },
                    { Event.KnownDataKeys.UserDescription, new UserDescription("noreply@exceptionless.io", "Blake ate two boxes of cookies and needs help") }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeNotFoundAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Source = "[GET] /not-found?page=20",
                Type = Event.KnownTypes.NotFound
            });
        }

        [Fact]
        public Task SendEventNoticeFeatureAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Source = "My Feature Usage",
                Value = 1,
                Type = Event.KnownTypes.FeatureUsage
            });
        }

        [Fact]
        public Task SendEventNoticeEmptyLogEventAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Value = 1,
                Type = Event.KnownTypes.Log
            });
        }

        [Fact]
        public Task SendEventNoticeLogMessageAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "Only Message",
                Type = Event.KnownTypes.Log
            });
        }

        [Fact]
        public Task SendEventNoticeLogSourceAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Source = "Only Source",
                Type = Event.KnownTypes.Log
            });
        }

        [Fact]
        public Task SendEventNoticeLogReallyLongSourceAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Source = "Soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooorce",
                Type = Event.KnownTypes.Log
            });
        }

        [Fact]
        public Task SendEventNoticeLogMessageSourceLevelAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "My Message",
                Source = "My Source",
                Type = Event.KnownTypes.Log,
                Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Level, "Warn" }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeDefaultAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "Default Test Message",
                Source = "Default Test Source"
            });
        }

        private async Task SendEventNoticeAsync(PersistentEvent ev) {
            var user = UserData.GenerateSampleUser();
            var project = ProjectData.GenerateSampleProject();

            ev.Id = TestConstants.EventId;
            ev.OrganizationId = TestConstants.OrganizationId;
            ev.ProjectId = TestConstants.ProjectId;
            ev.StackId = TestConstants.StackId;

            await _mailer.SendEventNoticeAsync(user, ev, project, RandomData.GetBool(), RandomData.GetBool(), 1);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendOrganizationAddedAsync() {
            var user = UserData.GenerateSampleUser();
            var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);

            await _mailer.SendOrganizationAddedAsync(user, organization, user);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendOrganizationInviteAsync() {
            var user = UserData.GenerateSampleUser();
            var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);

            await _mailer.SendOrganizationInviteAsync(user, organization, new Invite {
                DateAdded = SystemClock.UtcNow,
                EmailAddress = _options.EmailOptions.TestEmailAddress,
                Token = "1"
            });

            await RunMailJobAsync();

            if (GetService<IMailSender>() is InMemoryMailSender sender)
                Assert.Contains("Join Organization", sender.LastMessage.Body);
        }

        [Fact]
        public async Task SendOrganizationHourlyOverageNoticeAsync() {
            var user = UserData.GenerateSampleUser();
            var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);

            await _mailer.SendOrganizationNoticeAsync(user, organization, false, true);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendOrganizationMonthlyOverageNoticeAsync() {
            var user = UserData.GenerateSampleUser();
            var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);

            await _mailer.SendOrganizationNoticeAsync(user, organization, true, false);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendOrganizationPaymentFailedAsync() {
            var user = UserData.GenerateSampleUser();
            var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);

            await _mailer.SendOrganizationPaymentFailedAsync(user, organization);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendProjectDailySummaryAsync() {
            var user = UserData.GenerateSampleUser();
            var project = ProjectData.GenerateSampleProject();
            var mostFrequent = StackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error);

            await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, null, SystemClock.UtcNow.Date, true, 12, 1, 0, 1, 0, 0, false);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendProjectDailySummaryWithAllBlockedAsync() {
            var user = UserData.GenerateSampleUser();
            var project = ProjectData.GenerateSampleProject();
            var mostFrequent = StackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error);

            await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, null, SystemClock.UtcNow.Date, true, 123456, 1, 0, 1, 123456, 0, false);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendProjectDailySummaryNotConfiguredAsync() {
            var user = UserData.GenerateSampleUser();
            var project = ProjectData.GenerateSampleProject();

            await _mailer.SendProjectDailySummaryAsync(user, project, null, null, SystemClock.UtcNow.Date, false, 0, 0, 0, 0, 0, 0, false);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendProjectDailySummaryWithNoEventsButHasFixedEventsAsync() {
            var user = UserData.GenerateSampleUser();
            var project = ProjectData.GenerateSampleProject();

            await _mailer.SendProjectDailySummaryAsync(user, project, null, null, SystemClock.UtcNow.Date, true, 0, 0, 0, 10, 0, 0, false);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendProjectDailySummaryWithNoEventsButHasFixedAndTooBigEventsAsync() {
            var user = UserData.GenerateSampleUser();
            var project = ProjectData.GenerateSampleProject();

            await _mailer.SendProjectDailySummaryAsync(user, project, null, null, SystemClock.UtcNow.Date, true, 0, 0, 0, 10, 123456, 23, false);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendProjectDailySummaryWithFreeProjectAsync() {
            var user = UserData.GenerateSampleUser();
            var project = ProjectData.GenerateSampleProject();
            var mostFrequent = StackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error);
            var newest = StackData.GenerateStacks(1, generateId: true, type: Event.KnownTypes.Error);

            await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, newest, SystemClock.UtcNow.Date, true, 12, 1, 1, 2, 0, 0, true);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendUserPasswordResetAsync() {
            var user = UserData.GenerateSampleUser();
            user.CreatePasswordResetToken();

            await _mailer.SendUserPasswordResetAsync(user);
            await RunMailJobAsync();
        }

        [Fact]
        public async Task SendUserEmailVerifyAsync() {
            var user = UserData.GenerateSampleUser();
            user.CreateVerifyEmailAddressToken();

            await _mailer.SendUserEmailVerifyAsync(user);
            await RunMailJobAsync();
        }

        private async Task RunMailJobAsync() {
            var job = GetService<MailMessageJob>();
            await job.RunAsync();

            if (!(GetService<IMailSender>() is InMemoryMailSender sender))
                return;

            _logger.LogTrace("To:      {To}", sender.LastMessage.To);
            _logger.LogTrace("Subject: {Subject}", sender.LastMessage.Subject);
            _logger.LogTrace("Body:\n{Body}", sender.LastMessage.Body);
        }

        private Exception GetException() {
            void TestInner() {
                void TestInnerInner() {
                    throw new ApplicationException("Random Test Exception");
                }

                TestInnerInner();
            }

            try {
                TestInner();
            } catch (Exception ex) {
                return ex;
            }

            return null;
        }
    }
}