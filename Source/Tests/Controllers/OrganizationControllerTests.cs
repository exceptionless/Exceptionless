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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Mvc;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Membership;
using Exceptionless.Models;
using Exceptionless.Tests.Controllers.Base;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Controllers {
    public class OrganizationControllerTests : AuthenticatedMongoApiControllerBase<Organization, HttpResponseMessage, IOrganizationRepository> {
        private static readonly UserRepository _userRepository = IoC.GetInstance<IUserRepository>() as UserRepository;
        private static readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private static readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private static readonly BillingManager _billingManager = IoC.GetInstance<BillingManager>();

        public OrganizationControllerTests() : base(_organizationRepository, true) {
            SetUserWithAllRoles();
        }

        [Fact]
        public void UpdateOrganizationNameTest() {
            var organization = Repository.GetById(TestConstants.OrganizationId);
            Assert.NotNull(organization);

            var newName = organization.Name + 1;
            HttpResponseMessage response = PatchResponse(TestConstants.OrganizationId, new { Name = organization.Name + 1 });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(newName, Repository.GetById(TestConstants.OrganizationId).Name);
        }

        [Fact]
        public void UpdateRestrictedOrganizationPropertyTest() {
            var organization = Repository.GetById(TestConstants.OrganizationId);
            Assert.NotNull(organization);

            HttpResponseMessage response = PatchResponse(TestConstants.OrganizationId, new { ProjectCount = 10 });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                ProjectCount = organization.ProjectCount
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                StackCount = 10
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                StackCount = organization.StackCount
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                ErrorCount = 10
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                ErrorCount = organization.ErrorCount
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                TotalErrorCount = 10
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                TotalErrorCount = organization.TotalErrorCount
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                StripeCustomerId = "15"
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                StripeCustomerId = organization.StripeCustomerId
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                PlanId = "15"
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                PlanId = organization.PlanId
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                CardLast4 = "15"
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                CardLast4 = organization.CardLast4
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                SubscribeDate = DateTime.Now
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                SubscribeDate = organization.SubscribeDate
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                BillingChangeDate = DateTime.Now
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                BillingChangeDate = organization.BillingChangeDate
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                RetentionDays = 10
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                RetentionDays = organization.RetentionDays
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                MaxErrorsPerMonth = 10
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                MaxErrorsPerMonth = organization.MaxErrorsPerMonth
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                MaxProjects = 10
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.OrganizationId, new {
                MaxProjects = organization.MaxProjects
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void DeleteOrganizationWithInvalidUserTest() {
            SetUserWithNoRoles();

            HttpResponseMessage response = DeleteResponse(TestConstants.OrganizationId3);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            SetUserWithAllRoles();
        }

        [Fact]
        public void DeleteOrganizationTest() {
            SetUserWithNoRoles();

            List<Project> projects = _projectRepository.GetByOrganizationId(TestConstants.OrganizationId2).ToList();
            Assert.True(projects.Any());
            Assert.True(_userRepository.GetByOrganizationId(TestConstants.OrganizationId2).Any());

            HttpResponseMessage response = DeleteResponse(TestConstants.OrganizationId2);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            foreach (Project project in projects)
                _projectRepository.Delete(project);

            response = DeleteResponse(TestConstants.OrganizationId2);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.False(_projectRepository.GetByOrganizationId(TestConstants.OrganizationId2).Any());
            Assert.False(_userRepository.GetByOrganizationId(TestConstants.OrganizationId2).Any());

            SetUserWithAllRoles();
        }

        [Fact]
        public void RemoveUserWithInvalidOrganizationIdTest() {
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(String.Concat(_baseUrl, "/", TestConstants.InvalidOrganizationId, "/removeuser?emailAddress=invalid@emailaddress.com")), verb: HttpVerbs.Delete);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public void RemoveUserWithInvalidEmailAddressTest() {
            string removeUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId, "/removeuser?emailAddress=");
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(removeUserUri), verb: HttpVerbs.Delete);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public void RemoveUserWithInvalidUserTest() {
            string removeUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId, "/removeuser?emailAddress=", TestConstants.InvalidUserEmail);
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(removeUserUri), verb: HttpVerbs.Delete);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void RemoveUserWithValidInviteTest() {
            string removeUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId, "/removeuser?emailAddress=", TestConstants.InvitedOrganizationUserEmail);
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(removeUserUri), verb: HttpVerbs.Delete);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void RemoveUserWithInvalidInviteTest() {
            string removeUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId, "/removeuser?emailAddress=", TestConstants.InvalidInvitedOrganizationUserEmail);
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(removeUserUri), verb: HttpVerbs.Delete);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void RemoveUsersFromOrganizationTest() {
            string removeUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId, "/removeuser?emailAddress=");

            Assert.True(_userRepository.GetById(TestConstants.UserIdWithNoRoles).OrganizationIds.Contains(TestConstants.OrganizationId));
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(String.Concat(removeUserUri, TestConstants.UserEmailWithNoRoles)), verb: HttpVerbs.Delete);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(_userRepository.GetById(TestConstants.UserIdWithNoRoles).OrganizationIds.Contains(TestConstants.OrganizationId));

            // TODO: Check to see if the users notification settings were removed.

            Assert.True(_userRepository.GetById(TestConstants.UserId).OrganizationIds.Contains(TestConstants.OrganizationId));
            response = CreateResponse<HttpResponseMessage>(uri: new Uri(String.Concat(removeUserUri, TestConstants.UserEmail)), verb: HttpVerbs.Delete);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True(_userRepository.GetById(TestConstants.UserId).OrganizationIds.Contains(TestConstants.OrganizationId));
        }

        [Fact]
        public void InviteUserWithInvalidOrganizationIdTest() {
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(String.Concat(_baseUrl, "/", TestConstants.InvalidOrganizationId, "/invite?emailAddress=invalid@emailaddress.com")), verb: HttpVerbs.Post);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public void InviteUserWithInvalidEmailAddressTest() {
            string removeUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId, "/invite?emailAddress=");
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(removeUserUri), verb: HttpVerbs.Post);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public void InviteExistingUserToOrganizationTest() {
            string inviteUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId, "/invite?emailAddress=");

            Assert.False(_userRepository.GetById(TestConstants.UserId2).OrganizationIds.Contains(TestConstants.OrganizationId));
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(String.Concat(inviteUserUri, TestConstants.UserEmail2)), verb: HttpVerbs.Post);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(_userRepository.GetById(TestConstants.UserId2).OrganizationIds.Contains(TestConstants.OrganizationId));
        }

        [Fact]
        public void InviteUserToOrganizationTest() {
            string inviteUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId, "/invite?emailAddress=");

            Assert.Null(Repository.GetById(TestConstants.OrganizationId).Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, TestConstants.InvitedOrganizationUserEmail2)));
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(String.Concat(inviteUserUri, TestConstants.InvitedOrganizationUserEmail2)), verb: HttpVerbs.Post);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(Repository.GetById(TestConstants.OrganizationId).Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, TestConstants.InvitedOrganizationUserEmail2)));
        }

        [Fact]
        public void CreateSecondFreeOrganizationTest() {
            Organization organization = OrganizationData.GenerateOrganization(true);
            Assert.False(_userRepository.GetById(TestConstants.UserId2).OrganizationIds.Contains(organization.Id));
            HttpResponseMessage response = PostResponse(organization);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
            Assert.False(_userRepository.GetById(TestConstants.UserId2).OrganizationIds.Contains(organization.Id));
        }

        [Fact]
        public void InviteExistingUserToFreeOrganizationTest() {
            string inviteUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId3, "/invite?emailAddress=");

            Assert.False(_userRepository.GetById(TestConstants.UserId2).OrganizationIds.Contains(TestConstants.OrganizationId));
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(String.Concat(inviteUserUri, TestConstants.UserEmail2)), verb: HttpVerbs.Post);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
            Assert.False(_userRepository.GetById(TestConstants.UserId2).OrganizationIds.Contains(TestConstants.OrganizationId));
        }

        [Fact]
        public void InviteUserToFreeOrganizationTest() {
            string inviteUserUri = String.Concat(_baseUrl, "/", TestConstants.OrganizationId3, "/invite?emailAddress=");

            Assert.Null(Repository.GetById(TestConstants.OrganizationId).Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, TestConstants.InvitedOrganizationUserEmail2)));
            var response = CreateResponse<HttpResponseMessage>(uri: new Uri(String.Concat(inviteUserUri, TestConstants.InvitedOrganizationUserEmail2)), verb: HttpVerbs.Post);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
            Assert.Null(Repository.GetById(TestConstants.OrganizationId).Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, TestConstants.InvitedOrganizationUserEmail2)));
        }

        protected override void CreateData() {
            foreach (Organization organization in OrganizationData.GenerateSampleOrganizations()) {
                if (organization.Id == TestConstants.OrganizationId3)
                    _billingManager.ApplyBillingPlan(organization, BillingManager.FreePlan, UserData.GenerateSampleUser());
                else
                    _billingManager.ApplyBillingPlan(organization, BillingManager.SmallPlan, UserData.GenerateSampleUser());

                Repository.Add(organization);
            }

            foreach (Project project in ProjectData.GenerateSampleProjects()) {
                var organization = Repository.GetById(project.OrganizationId);
                organization.ProjectCount += 1;
                Repository.Update(organization);

                _projectRepository.Add(project);
            }

            var membershipProvider = new MembershipProvider(_userRepository.Collection);
            foreach (User user in UserData.GenerateSampleUsers()) {
                if (user.Id == TestConstants.UserId) {
                    user.OrganizationIds.Add(TestConstants.OrganizationId2);
                    user.OrganizationIds.Add(TestConstants.OrganizationId3);
                }

                if (user.Id == TestConstants.UserIdWithNoRoles)
                    user.OrganizationIds.Add(TestConstants.OrganizationId2);

                membershipProvider.CreateAccount(user);
            }
        }

        protected override void RemoveData() {
            base.RemoveData();
            _userRepository.DeleteAll();
            _projectRepository.DeleteAll();
        }
    }
}