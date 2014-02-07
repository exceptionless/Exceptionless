#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net.Http;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Pipeline;
using Exceptionless.Membership;
using Exceptionless.Models;
using Exceptionless.Tests.Controllers.Base;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Pipeline {
    public class ErrorPipelineTests : AuthenticatedMongoApiControllerBase<Error, HttpResponseMessage, ErrorRepository> {
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly IErrorStackRepository _errorStackRepository = IoC.GetInstance<IErrorStackRepository>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();
        private readonly BillingManager _billingManager = IoC.GetInstance<BillingManager>();

        public ErrorPipelineTests() : base(IoC.GetInstance<ErrorRepository>(), true) {}

        [Fact]
        public void ProcessErrorTest() {
            Error error = ErrorData.GenerateError(id: TestConstants.ErrorId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);

            var organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.ProjectCount);
            Assert.Equal(0, organization.StackCount);
            Assert.Equal(0, organization.ErrorCount);
            Assert.Equal(0, organization.TotalErrorCount);

            var project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(0, project.StackCount);
            Assert.Equal(0, project.ErrorCount);
            Assert.Equal(0, project.TotalErrorCount);

            var pipeline = IoC.GetInstance<ErrorPipeline>();
            Exception exception = Record.Exception(() => pipeline.Run(error));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(1, organization.ErrorCount);
            Assert.Equal(1, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(1, project.ErrorCount);
            Assert.Equal(1, project.TotalErrorCount);

            exception = Record.Exception(() => pipeline.Run(error));
            Assert.Null(exception);
            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(1, organization.ErrorCount);
            Assert.Equal(1, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(1, project.ErrorCount);
            Assert.Equal(1, project.TotalErrorCount);

            error.Id = TestConstants.ErrorId2;
            exception = Record.Exception(() => pipeline.Run(error));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(2, organization.ErrorCount);
            Assert.Equal(2, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(2, project.ErrorCount);
            Assert.Equal(2, project.TotalErrorCount);

            exception = Record.Exception(() => pipeline.Run(ErrorData.GenerateSampleError(TestConstants.ErrorId8)));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(2, organization.StackCount);
            Assert.Equal(3, organization.ErrorCount);
            Assert.Equal(3, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(2, project.StackCount);
            Assert.Equal(3, project.ErrorCount);
            Assert.Equal(3, project.TotalErrorCount);

            Repository.RemoveAllByErrorStackIdAsync(error.ErrorStackId).Wait();
            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(2, organization.StackCount);
            Assert.Equal(1, organization.ErrorCount);
            Assert.Equal(3, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(2, project.StackCount);
            Assert.Equal(1, project.ErrorCount);
            Assert.Equal(3, project.TotalErrorCount);
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
        }

        protected override void RemoveData() {
            base.RemoveData();

            _errorStackRepository.DeleteAll();
            _userRepository.DeleteAll();
            _projectRepository.DeleteAll();
            _organizationRepository.DeleteAll();
        }
    }
}