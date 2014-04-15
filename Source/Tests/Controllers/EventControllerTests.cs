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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Migrations.Documents;
using Exceptionless.Core.Queues;
using Exceptionless.Membership;
using Exceptionless.Models;
using Exceptionless.Tests.Controllers.Base;
using Exceptionless.Tests.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Tests.Controllers {
    public class EventControllerTests : AuthenticatedMongoApiControllerBase<Event, HttpResponseMessage, IEventRepository> {
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();
        private readonly BillingManager _billingManager = IoC.GetInstance<BillingManager>();
        private ExceptionlessMqServer _mqServer;

        public EventControllerTests() : base(IoC.GetInstance<IEventRepository>(), true) { }

        [Fact]
        public void Post() {
            SetValidApiKey();

            HttpResponseMessage response = PostResponse(EventData.GenerateSampleEvent(TestConstants.ErrorId3));
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            Assert.NotNull(response.Headers);
            KeyValuePair<string, IEnumerable<string>> header = response.Headers.FirstOrDefault(h => String.Equals(h.Key, "Location", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(header);
            Assert.NotNull(header.Value);
            Assert.Equal(1, header.Value.Count());
            Assert.NotNull(header.Value.First());

            Assert.Contains(String.Concat("error/", TestConstants.ErrorId3), header.Value.First());
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\EventData\", "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { file });

                return result.ToArray();
            }
        }

        private HttpResponseMessage GetResponseMessage(string id) {
            return CreateResponse<HttpResponseMessage>(id: id);
        }

        private int ConfigurationVersion { get; set; }

        protected override void SetUp() {
            base.SetUp();

            _mqServer = IoC.GetInstance<ExceptionlessMqServer>();
            _mqServer.Start();
        }

        protected override void TearDown() {
            base.TearDown();
            if (_mqServer != null)
                _mqServer.Dispose();
        }

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
                var organization = _organizationRepository.GetById(project.OrganizationId);
                organization.ProjectCount += 1;
                _organizationRepository.Update(organization);

                _projectRepository.Add(project);
            }

            var membershipProvider = new MembershipProvider(_userRepository.Collection);
            foreach (User user in UserData.GenerateSampleUsers()) {
                if (user.Id == TestConstants.UserId) {
                    user.OrganizationIds.Add(TestConstants.OrganizationId2);
                    user.OrganizationIds.Add(TestConstants.OrganizationId3);
                }

                membershipProvider.CreateAccount(user);
            }

            Repository.Add(EventData.GenerateSampleEvents());
        }

        protected override void RemoveData() {
            base.RemoveData();

            _userRepository.DeleteAll();
            _projectRepository.DeleteAll();
            _organizationRepository.DeleteAll();
        }
    }
}