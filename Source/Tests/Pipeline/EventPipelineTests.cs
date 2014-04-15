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
using System.Net.Http;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Pipeline;
using Exceptionless.Membership;
using Exceptionless.Models;
using Exceptionless.Tests.Controllers.Base;
using Exceptionless.Tests.Utility;
using Newtonsoft.Json;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Tests.Pipeline {
    public class EventPipelineTests : AuthenticatedMongoApiControllerBase<Event, HttpResponseMessage, EventRepository> {
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();
        private readonly BillingManager _billingManager = IoC.GetInstance<BillingManager>();

        public EventPipelineTests() : base(IoC.GetInstance<EventRepository>(), true) {}

        [Fact]
        public void VerifyOrganizationAndProjectStatistics() {
            Event ev = EventData.GenerateEvent(id: TestConstants.ErrorId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);

            var organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.ProjectCount);
            Assert.Equal(0, organization.StackCount);
            Assert.Equal(0, organization.ErrorCount);
            Assert.Equal(0, organization.TotalErrorCount);

            var project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(0, project.StackCount);
            Assert.Equal(0, project.ErrorCount);
            Assert.Equal(0, project.TotalErrorCount);

            var pipeline = IoC.GetInstance<EventPipeline>();
            Exception exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(1, organization.ErrorCount);
            Assert.Equal(1, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(1, project.ErrorCount);
            Assert.Equal(1, project.TotalErrorCount);

            exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);
            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(1, organization.ErrorCount);
            Assert.Equal(1, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(1, project.ErrorCount);
            Assert.Equal(1, project.TotalErrorCount);

            ev.Id = TestConstants.ErrorId2;
            exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(2, organization.ErrorCount);
            Assert.Equal(2, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(2, project.ErrorCount);
            Assert.Equal(2, project.TotalErrorCount);

            exception = Record.Exception(() => pipeline.Run(EventData.GenerateSampleEvent(TestConstants.ErrorId8)));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(2, organization.StackCount);
            Assert.Equal(3, organization.ErrorCount);
            Assert.Equal(3, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(2, project.StackCount);
            Assert.Equal(3, project.ErrorCount);
            Assert.Equal(3, project.TotalErrorCount);

            Repository.RemoveAllByStackId(ev.StackId);
            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(2, organization.StackCount);
            Assert.Equal(1, organization.ErrorCount);
            Assert.Equal(3, organization.TotalErrorCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(2, project.StackCount);
            Assert.Equal(1, project.ErrorCount);
            Assert.Equal(3, project.TotalErrorCount);
        }

        [Fact]
        public void SyncEventStackTags() {
            const string Tag1 = "Tag One";
            const string Tag2 = "Tag Two";
            const string Tag2_Lowercase = "tag two";

            Event ev = EventData.GenerateEvent(id: TestConstants.ErrorId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);
            ev.Tags = new TagSet { Tag1 };

            var pipeline = IoC.GetInstance<EventPipeline>();
            Assert.DoesNotThrow(() => pipeline.Run(ev));

            ev = Repository.GetById(ev.Id);
            Assert.NotNull(ev);
            Assert.NotNull(ev.StackId);

            var stack = _stackRepository.GetById(ev.StackId);
            Assert.Equal(new TagSet { Tag1 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);
            ev.Tags = new TagSet { Tag2 };

            Assert.DoesNotThrow(() => pipeline.Run(ev));
            stack = _stackRepository.GetById(ev.StackId);
            Assert.Equal(new TagSet { Tag1, Tag2 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);
            ev.Tags = new TagSet { Tag2_Lowercase };

            Assert.DoesNotThrow(() => pipeline.Run(ev));
            stack = _stackRepository.GetById(ev.StackId);
            Assert.Equal(new TagSet { Tag1, Tag2 }, stack.Tags);
        }

        [Theory]
        [PropertyData("Events")]
        public void ProcessEvents(string errorFilePath) {
            // TODO: We currently fail to process this error due to https://jira.mongodb.org/browse/CSHARP-930
            if (errorFilePath.Contains("881")) 
                return;

            var pipeline = IoC.GetInstance<EventPipeline>();
            var ev = JsonConvert.DeserializeObject<Event>(File.ReadAllText(errorFilePath));
            Assert.NotNull(ev);
            ev.ProjectId = TestConstants.ProjectId;
            ev.OrganizationId = TestConstants.OrganizationId;

            Assert.DoesNotThrow(() => pipeline.Run(ev));
        }

        public static IEnumerable<object[]> Events {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\EventData\", "*.expected.json", SearchOption.AllDirectories))
                    result.Add(new object[] { file });

                return result.ToArray();
            }
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

            _stackRepository.DeleteAll();
            _userRepository.DeleteAll();
            _projectRepository.DeleteAll();
            _organizationRepository.DeleteAll();
        }
    }
}