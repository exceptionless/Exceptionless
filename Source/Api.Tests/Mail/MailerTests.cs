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
using CodeSmith.Core.Extensions;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Mail {
    public class MailerTests {
        [Fact(Skip = "Used for testing html formatting.")]
        public void SendNotification() {
            var mailer = IoC.GetInstance<Mailer>();
            mailer.SendNotice(Settings.Current.TestEmailAddress, new ErrorNotificationModel {
                BaseUrl = "http://app.exceptionless.com",
                Code = "500",
                ErrorId = "1",
                ErrorStackId = "1",
                FullTypeName = "SomeError",
                IsCritical = true,
                IsNew = true,
                IsRegression = false,
                Message = "Happy days are here again...",
                ProjectId = "1",
                ProjectName = "Test",
                Subject = "An error has occurred.",
                TotalOccurrences = 1,
                Url = "http://app.exceptionless.com",
                UserAgent = "eric"
            });
        }

        [Fact(Skip = "Used for testing html formatting.")]
        public void SendInvite() {
            var mailer = IoC.GetInstance<Mailer>();
            User user = UserData.GenerateSampleUser();
            Organization organization = OrganizationData.GenerateSampleOrganization();
            mailer.SendInvite(user, organization, new Invite {
                DateAdded = DateTime.Now,
                EmailAddress = Settings.Current.TestEmailAddress,
                Token = "1"
            });
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
                MostFrequent = new List<ErrorStackResult> {
                    new ErrorStackResult {
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
                New = new List<ErrorStack> {
                    new ErrorStack {
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