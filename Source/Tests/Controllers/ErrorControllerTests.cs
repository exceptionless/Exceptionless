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
    public class ErrorControllerTests : AuthenticatedMongoApiControllerBase<Error, HttpResponseMessage, IEventRepository> {
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();
        private readonly BillingManager _billingManager = IoC.GetInstance<BillingManager>();
        private ExceptionlessMqServer _mqServer;

        public ErrorControllerTests() : base(IoC.GetInstance<IEventRepository>(), true) {}

        [Fact]
        public void GetAll() {
            SetUserWithAllRoles();

            IEnumerable<Error> errors = GetAllResponse();
            Assert.NotNull(errors);
            Assert.Equal(4, errors.Count());
        }

        [Fact]
        public void GetAllWithClientApiKey() {
            SetValidApiKey();

            var response = CreateResponse<HttpResponseMessage>();
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            Assert.Null(GetResponse(TestConstants.ErrorId7));
        }

        [Fact]
        public void GetById() {
            SetUserWithAllRoles();

            Error error = GetResponse(TestConstants.ErrorId);
            Assert.NotNull(error);
            Assert.Equal(TestConstants.ErrorId, error.Id);
            Assert.NotNull(error.Message);

            // Child object
            Assert.NotNull(error.Inner);
            Assert.NotNull(error.Inner.Message);

            // Dictionary<string, object>
            Assert.NotNull(error.ExtendedData);
            Assert.NotEqual(0, error.ExtendedData.Count);

            // Collection<StackFrame> 
            Assert.NotNull(error.StackTrace);
            Assert.NotEqual(0, error.StackTrace.Count);

            // HashSet<string>
            Assert.NotNull(error.Tags);
            Assert.NotEqual(0, error.Tags.Count);
        }

        [Fact]
        public void GetByIdWithClientApiKey() {
            SetValidApiKey();

            HttpResponseMessage response = GetResponseMessage(TestConstants.ErrorId7);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            Assert.Null(GetResponse(TestConstants.ErrorId7));
        }

        [Fact]
        public void GetByIdWithApiKey() {
            SetValidApiKey();

            HttpResponseMessage response = GetResponseMessage(TestConstants.ErrorId);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            Assert.Null(GetResponse(TestConstants.ErrorId));
        }

        [Fact]
        public void GetByIdNonExistentError() {
            SetUserWithAllRoles();

            HttpResponseMessage response = GetResponseMessage(TestConstants.InvalidErrorId);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            Error error = GetResponse(TestConstants.InvalidErrorId);
            Assert.Null(error);
        }

        [Fact]
        public void Post() {
            SetValidApiKey();

            HttpResponseMessage response = PostResponse(ErrorData.GenerateSampleError(TestConstants.ErrorId3));
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

        [Fact]
        public void PostExtremelyLargeError() {
            SetValidApiKey();

            var error = ErrorData.GenerateError(id: TestConstants.ErrorId3);
            for (int i = 0; i < 4; i++)
                error.ExtendedData.Add(Guid.NewGuid().ToString(), new string('x', 512 * 1024));

            HttpResponseMessage response = PostResponse(error);
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

        [Fact]
        public void PostDuplicate() {
            SetValidApiKey();

            HttpResponseMessage response = PostResponse(ErrorData.GenerateSampleError(TestConstants.ErrorId4));
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            response = PostResponse(ErrorData.GenerateSampleError(TestConstants.ErrorId4));
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Theory]
        [PropertyData("Errors")]
        public void PostJsonErrors(string errorFilePath) {
            SetValidApiKey();

            JObject jObject = JObject.Parse(File.ReadAllText(errorFilePath));
            Assert.NotNull(jObject);

            DocumentUpgrader.Current.Upgrade<Error>(jObject);

            Error error = null;
            Assert.Null(Record.Exception(() => error = JsonConvert.DeserializeObject<Error>(jObject.ToString())));
            Assert.NotNull(error);

            HttpResponseMessage response = PostResponse(error);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public void PostWithInvalidApiKey() {
            SetInvalidApiKey();

            HttpResponseMessage response = PostResponse(ErrorData.GenerateSampleError(TestConstants.ErrorId3));
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public void PostWithSuspendedApiKey() {
            SetSuspendedApiKey();

            HttpResponseMessage response = PostResponse(ErrorData.GenerateSampleError(TestConstants.ErrorId3));
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public void Put() {
            SetUserWithAllRoles();

            Error error = GetResponse(TestConstants.ErrorId);
            Assert.NotNull(error);

            error.UserDescription = "Desktop";

            HttpResponseMessage response = PutResponse(TestConstants.ErrorId, error);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.InRange(ConfigurationVersion, 0, int.MaxValue);
        }

        [Fact]
        public void PutNonExistentError() {
            SetUserWithAllRoles();

            Error error = ErrorData.GenerateSampleError(TestConstants.InvalidErrorId);
            error.UserDescription = "Desktop";

            HttpResponseMessage response = PutResponse(TestConstants.InvalidErrorId, error);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public void PutInvalidOrganizationError() {
            SetUserWithAllRoles();

            Error error = GetResponse(TestConstants.ErrorId);
            Assert.NotNull(error);

            HttpResponseMessage response = PutResponse(TestConstants.ErrorId, error);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            error.OrganizationId = TestConstants.InvalidOrganizationId;
            response = PutResponse(TestConstants.ErrorId, error);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public void PutWithClientApiKey() {
            SetValidApiKey();

            Error error = ErrorData.GenerateSampleError(TestConstants.ErrorId7);
            HttpResponseMessage response = PutResponse(TestConstants.ErrorId7, error);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            error.OrganizationId = TestConstants.InvalidOrganizationId;
            response = PutResponse(TestConstants.ErrorId7, error);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public void Patch() {
            SetValidApiKey();

            HttpResponseMessage response = PatchResponse(TestConstants.ErrorId3, new {
                UserEmail = "some@email.com"
            });
            Assert.NotNull(response);

            if (response.StatusCode != HttpStatusCode.OK) {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                HttpResponseMessage post = PostResponse(ErrorData.GenerateSampleError(TestConstants.ErrorId3));
                Assert.Equal(HttpStatusCode.Created, post.StatusCode);

                Thread.Sleep(500);
                response = PatchResponse(TestConstants.ErrorId3, new {
                    UserEmail = "some@email.com"
                });
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void PatchNonExistentError() {
            SetUserWithAllRoles();

            Error result = GetResponse(TestConstants.InvalidErrorId);
            Assert.Null(result);

            HttpResponseMessage response = PatchResponse(TestConstants.InvalidErrorId, new {
                UserEmail = "some@email.com"
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public void PatchInvalidOrganizationError() {
            SetUserWithAllRoles();

            Error error = GetResponse(TestConstants.ErrorId);
            Assert.NotNull(error);

            HttpResponseMessage response = PatchResponse(TestConstants.ErrorId, new {
                UserDescription = "My Desktop"
            });

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = PatchResponse(TestConstants.ErrorId, new {
                OrganizationId = TestConstants.InvalidOrganizationId
            });
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public void Delete() {
            SetUserWithAllRoles();

            Error result = GetResponse(TestConstants.ErrorId3);
            HttpResponseMessage response = DeleteResponse(TestConstants.ErrorId3);
            Assert.NotNull(response);

            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            //if (result == null)
            //    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            //else
            //    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            //// Ensure it was deleted.
            //Assert.Null(GetResponse(TestConstants.ErrorId3));
        }

        [Fact]
        public void DeleteNonExistentError() {
            SetUserWithAllRoles();

            Assert.Null(GetResponse(TestConstants.ErrorId3));
            HttpResponseMessage response = DeleteResponse(TestConstants.InvalidErrorId);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            //Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public void DeleteWithClientApiKey() {
            SetValidApiKey();

            Assert.Null(GetResponse(TestConstants.ErrorId7));
            HttpResponseMessage response = DeleteResponse(TestConstants.ErrorId7);
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            //Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public void Batch() {
            // TODO: Add batch unit tests.
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\ErrorData\", "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
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

            Repository.Add(ErrorData.GenerateSampleErrors());
        }

        protected override void RemoveData() {
            base.RemoveData();

            _userRepository.DeleteAll();
            _projectRepository.DeleteAll();
            _organizationRepository.DeleteAll();
        }
    }
}