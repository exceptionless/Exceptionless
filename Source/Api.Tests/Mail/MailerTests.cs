#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using AutoMapper;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Enrichments;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Mail {
    public class MailerTests {
        public MailerTests() {
            Mapper.CreateMap<Event, PersistentEvent>();
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendLogNotification() {
            var mailer = IoC.GetInstance<Mailer>();
            mailer.SendNotice(Settings.Current.TestEmailAddress, new EventNotification {
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
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendNotFoundNotification() {
            var mailer = IoC.GetInstance<Mailer>();
            mailer.SendNotice(Settings.Current.TestEmailAddress, new EventNotification {
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
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendSimpleErrorNotification() {
            PersistentEvent ev = null;
            var client = new ExceptionlessClient("123456789");
            try {
                throw new Exception("Happy days are here again...");
            } catch (Exception ex) {
                var builder = ex.ToExceptionless(client: client);
                EventEnrichmentManager.Enrich(new EventEnrichmentContext(client, builder.EnrichmentContextData), builder.Target);
                ev = Mapper.Map<PersistentEvent>(builder.Target);
            }

            ev.Id = "1";
            ev.OrganizationId = "1";
            ev.ProjectId = "1";
            ev.StackId = "1";

            var mailer = IoC.GetInstance<Mailer>();
            mailer.SendNotice(Settings.Current.TestEmailAddress, new EventNotification {
                Event = ev,
                IsNew = true,
                IsCritical = true,
                IsRegression = false,
                TotalOccurrences = 1,
                ProjectName = "Testing"
            });
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendErrorNotification() {
            PersistentEvent ev = null;
            var client = new ExceptionlessClient(c => {
                c.ApiKey = "123456789";
                c.UseErrorEnrichment();
            });
            try {
                throw new Exception("Happy days are here again...");
            } catch (Exception ex) {
                var builder = ex.ToExceptionless(client: client);
                EventEnrichmentManager.Enrich(new EventEnrichmentContext(client, builder.EnrichmentContextData), builder.Target);
                ev = Mapper.Map<PersistentEvent>(builder.Target);
            }

            ev.Id = "1";
            ev.OrganizationId = "1";
            ev.ProjectId = "1";
            ev.StackId = "1";

            var mailer = IoC.GetInstance<Mailer>();
            mailer.SendNotice(Settings.Current.TestEmailAddress, new EventNotification {
                Event = ev,
                IsNew = true,
                IsCritical = true,
                IsRegression = false,
                TotalOccurrences = 1,
                ProjectName = "Testing"
            });
        }

        [Fact]
        public void SendInvite() {
            var mailer = IoC.GetInstance<Mailer>();
            var mailerSender = IoC.GetInstance<IMailSender>() as InMemoryMailSender;
            var mailJob = IoC.GetInstance<ProcessMailMessageJob>();
            Assert.NotNull(mailerSender);

            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            mailer.SendInvite(user, organization, new Invite {
                DateAdded = DateTime.Now,
                EmailAddress = Settings.Current.TestEmailAddress,
                Token = "1"
            });
            mailJob.Run();

            Assert.Equal(1, mailerSender.TotalSent);
            Assert.Equal(Settings.Current.TestEmailAddress, mailerSender.LastMessage.To);
            Assert.Contains("Join Organization", mailerSender.LastMessage.HtmlBody);
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendAddedToOrganization() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            mailer.SendAddedToOrganization(user, organization, user);
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendPasswordReset() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            mailer.SendPasswordReset(user);
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendVerifyEmail() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            mailer.SendVerifyEmail(user);
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendSummaryNotification() {
            var mailer = IoC.GetInstance<Mailer>();
            mailer.SendSummaryNotification(Settings.Current.TestEmailAddress, new SummaryNotificationModel {
                ProjectId = "1",
                BaseUrl = "http://app.exceptionless.com",
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
                EndDate = DateTime.Now.ToEndOfDay(),
                NewTotal = 1,
                PerHourAverage = 0.4,
                ProjectName = "Blah",
                Subject = "A daily summary",
                Total = 12,
                UniqueTotal = 1,
                HasSubmittedErrors = true,
                IsFreePlan = false
            });
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendPaymentFailed() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            mailer.SendPaymentFailed(user, organization);
        }
    }
}