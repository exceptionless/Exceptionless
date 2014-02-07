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
using System.Net;
using System.Net.Http;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Membership;
using Exceptionless.Models;
using Exceptionless.Tests.Controllers.Base;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Controllers {
    public class ProjectControllerTests : AuthenticatedMongoApiControllerBase<Project, HttpResponseMessage, IProjectRepository> {
        private readonly OrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>() as OrganizationRepository;
        private readonly UserRepository _userRepository = IoC.GetInstance<IUserRepository>() as UserRepository;
        private readonly BillingManager _billingManager = IoC.GetInstance<BillingManager>();

        public ProjectControllerTests() : base(IoC.GetInstance<IProjectRepository>(), true) {
            SetUserWithAllRoles();
        }

        [Fact]
        public void CreateProjectOnFreeOrganizationTest() {
            Assert.Equal(0, _organizationRepository.GetById(TestConstants.OrganizationId3).ProjectCount);

            Project project = ProjectData.GenerateProject(true, organizationId: TestConstants.OrganizationId3);
            HttpResponseMessage response = PostResponse(project);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            Assert.Equal(1, _organizationRepository.GetById(TestConstants.OrganizationId3).ProjectCount);

            project = ProjectData.GenerateProject(true, organizationId: TestConstants.OrganizationId3);
            response = PostResponse(project);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);

            Assert.Equal(1, _organizationRepository.GetById(TestConstants.OrganizationId3).ProjectCount);
        }

        [Fact]
        public void GetConfigurationClientApiKey() {
            foreach (string apiKey in new List<string> {
                TestConstants.ApiKey,
                TestConstants.ApiKey2,
                TestConstants.ApiKey3
            }) {
                SetApiKey(apiKey);
                var configuration = CreateResponse<ClientConfiguration>(uri: new Uri(String.Concat(_baseUrl, "/config")));
                Assert.NotNull(configuration);
                Assert.NotNull(configuration.Settings);
                Assert.Equal(0, configuration.Version);
                Assert.Equal(0, ConfigurationVersion);
            }

            SetUserWithAllRoles();
        }

        [Fact]
        public void UpdateProjectNameTest() {
            var project = Repository.GetById(TestConstants.ProjectId);
            Assert.NotNull(project);

            var newName = project.Name + 1;
            HttpResponseMessage response = PatchResponse(TestConstants.ProjectId, new { Name = project.Name + 1 });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(newName, Repository.GetById(TestConstants.ProjectId).Name);
        }

        [Fact]
        public void UpdateRestrictedProjectPropertyTest() {
            SetUserWithAllRoles();

            var project = Repository.GetById(TestConstants.ProjectId);
            Assert.NotNull(project);

            HttpResponseMessage response = PatchResponse(TestConstants.ProjectId, new { StackCount = 1 });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.ProjectId, new { StackCount = project.StackCount });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.ProjectId, new { ErrorCount = 1 });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.ProjectId, new { ErrorCount = project.ErrorCount });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.ProjectId, new { TotalErrorCount = 1 });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = PatchResponse(TestConstants.ProjectId, new { TotalErrorCount = project.TotalErrorCount });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // TODO: Add tests that modify the Configuration version.

        private int ConfigurationVersion { get; set; }

        protected override void OnResponseCreated(HttpResponseMessage response) {
            int version;
            if (response.TryGetConfigurationVersion(out version))
                ConfigurationVersion = version;
        }

        protected override void CreateData() {
            foreach (Organization organization in OrganizationData.GenerateSampleOrganizations()) {
                if (organization.Id == TestConstants.OrganizationId3)
                    _billingManager.ApplyBillingPlan(organization, BillingManager.FreePlan, UserData.GenerateSampleUser());
                else
                    _billingManager.ApplyBillingPlan(organization, BillingManager.SmallPlan, UserData.GenerateSampleUser());

                _organizationRepository.Add(organization);
            }

            foreach (Project project in ProjectData.GenerateSampleProjects()) {
                Organization organization = _organizationRepository.GetById(project.OrganizationId);
                organization.ProjectCount += 1;
                _organizationRepository.Update(organization);

                Repository.Add(project);
            }

            var membershipProvider = new MembershipProvider(_userRepository.Collection);
            foreach (User user in UserData.GenerateSampleUsers()) {
                if (user.Id == TestConstants.UserId) {
                    user.OrganizationIds.Add(TestConstants.OrganizationId2);
                    user.OrganizationIds.Add(TestConstants.OrganizationId3);
                }

                membershipProvider.CreateAccount(user);
            }
        }

        protected override void RemoveData() {
            base.RemoveData();
            _organizationRepository.DeleteAll();
            _userRepository.DeleteAll();
        }
    }
}