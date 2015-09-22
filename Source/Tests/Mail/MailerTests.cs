using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Mail {
    public class MailerTests {
        public MailerTests() {
            Mapper.CreateMap<Event, PersistentEvent>();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendLogNotification() {
            var mailer = IoC.GetInstance<Mailer>();
            await mailer.SendNoticeAsync(Settings.Current.TestEmailAddress, new EventNotification {
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
            }).AnyContext();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendNotFoundNotification() {
            var mailer = IoC.GetInstance<Mailer>();
            await mailer.SendNoticeAsync(Settings.Current.TestEmailAddress, new EventNotification {
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
            }).AnyContext();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendSimpleErrorNotification() {
            PersistentEvent ev = null;
            //var client = new ExceptionlessClient("123456789");
            //try {
            //    throw new Exception("Happy days are here again...");
            //} catch (Exception ex) {
            //    var builder = ex.ToExceptionless(client: client);
            //    EventEnrichmentManager.Enrich(new EventEnrichmentContext(client, builder.EnrichmentContextData), builder.Target);
            //    ev = Mapper.Map<PersistentEvent>(builder.Target);
            //}

            ev.Id = "1";
            ev.OrganizationId = "1";
            ev.ProjectId = "1";
            ev.StackId = "1";

            var mailer = IoC.GetInstance<Mailer>();
            await mailer.SendNoticeAsync(Settings.Current.TestEmailAddress, new EventNotification {
                Event = ev,
                IsNew = true,
                IsCritical = true,
                IsRegression = false,
                TotalOccurrences = 1,
                ProjectName = "Testing"
            }).AnyContext();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendErrorNotification() {
            PersistentEvent ev = null;
            //var client = new ExceptionlessClient(c => {
            //    c.ApiKey = "123456789";
            //    c.UseErrorEnrichment();
            //});
            //try {
            //    throw new Exception("Happy days are here again...");
            //} catch (Exception ex) {
            //    var builder = ex.ToExceptionless(client: client);
            //    EventEnrichmentManager.Enrich(new EventEnrichmentContext(client, builder.EnrichmentContextData), builder.Target);
            //    ev = Mapper.Map<PersistentEvent>(builder.Target);
            //}

            ev.Id = "1";
            ev.OrganizationId = "1";
            ev.ProjectId = "1";
            ev.StackId = "1";

            var mailer = IoC.GetInstance<Mailer>();
            await mailer.SendNoticeAsync(Settings.Current.TestEmailAddress, new EventNotification {
                Event = ev,
                IsNew = true,
                IsCritical = true,
                IsRegression = false,
                TotalOccurrences = 1,
                ProjectName = "Testing"
            }).AnyContext();
        }

        [Fact]
        public async Task SendInvite() {
            var mailer = IoC.GetInstance<Mailer>();
            var mailerSender = IoC.GetInstance<IMailSender>() as InMemoryMailSender;
            var mailJob = IoC.GetInstance<MailMessageJob>();
            Assert.NotNull(mailerSender);

            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            await mailer.SendInviteAsync(user, organization, new Invite {
                DateAdded = DateTime.Now,
                EmailAddress = Settings.Current.TestEmailAddress,
                Token = "1"
            }).AnyContext();
            await mailJob.RunAsync().AnyContext();

            Assert.Equal(1, mailerSender.TotalSent);
            Assert.Equal(Settings.Current.TestEmailAddress, mailerSender.LastMessage.To);
            Assert.Contains("Join Organization", mailerSender.LastMessage.HtmlBody);
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendAddedToOrganization() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            await mailer.SendAddedToOrganizationAsync(user, organization, user).AnyContext();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendPasswordReset() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            await mailer.SendPasswordResetAsync(user).AnyContext();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendVerifyEmail() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            await mailer.SendVerifyEmailAsync(user).AnyContext();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendSummaryNotification() {
            var mailer = IoC.GetInstance<Mailer>();
            await mailer.SendDailySummaryAsync(Settings.Current.TestEmailAddress, new DailySummaryModel {
                ProjectId = "1",
                BaseUrl = "http://be.exceptionless.io",
                MostFrequent = new List<EventStackResult> {
                    new EventStackResult {
                        First = DateTime.Now,
                        Id = "1",
                        Last = DateTime.Now,
                        Is404 = false,
                        Method = "Blah()",
                        Path = "/blah",
                        Title = "Some Error",
                        Total = 12,
                        Type = "SomeError"
                    }
                },
                New = new List<Stack> {
                    new Stack {
                        DateFixed = DateTime.Now,
                        Description = "Error 1",
                        FirstOccurrence = DateTime.Now,
                        IsRegressed = true,
                        LastOccurrence = DateTime.Now,
                        TotalOccurrences = 12
                    }
                },
                StartDate = DateTime.Now.Date,
                EndDate = DateTime.Now.EndOfDay(),
                NewTotal = 1,
                PerHourAverage = 0.4,
                ProjectName = "Blah",
                Subject = "A daily summary",
                Total = 12,
                UniqueTotal = 1,
                HasSubmittedEvents = true,
                IsFreePlan = false
            }).AnyContext();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public async Task SendPaymentFailed() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            await mailer.SendPaymentFailedAsync(user, organization).AnyContext();
        }
    }
}