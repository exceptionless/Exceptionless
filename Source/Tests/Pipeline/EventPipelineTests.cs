#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Component;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Pipeline {
    public sealed class EventPipelineTests : DisposableBase {
        private readonly IEventRepository _eventRepository = IoC.GetInstance<EventRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly BillingManager _billingManager = IoC.GetInstance<BillingManager>();

        public EventPipelineTests() {
            DisposeManagedResources();
            CreateData();
        }

        [Fact]
        public void VerifyOrganizationAndProjectStatistics() {
            var ev = EventData.GenerateEvent(id: TestConstants.EventId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);

            var organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.ProjectCount);
            Assert.Equal(0, organization.StackCount);
            Assert.Equal(0, organization.EventCount);
            Assert.Equal(0, organization.TotalEventCount);

            var project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(0, project.StackCount);
            Assert.Equal(0, project.EventCount);
            Assert.Equal(0, project.TotalEventCount);

            var pipeline = IoC.GetInstance<EventPipeline>();
            Exception exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(1, organization.EventCount);
            Assert.Equal(1, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(1, project.EventCount);
            Assert.Equal(1, project.TotalEventCount);

            exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);
            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(1, organization.EventCount);
            Assert.Equal(1, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(1, project.EventCount);
            Assert.Equal(1, project.TotalEventCount);

            ev.Id = TestConstants.EventId2;
            exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.StackCount);
            Assert.Equal(2, organization.EventCount);
            Assert.Equal(2, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.StackCount);
            Assert.Equal(2, project.EventCount);
            Assert.Equal(2, project.TotalEventCount);

            exception = Record.Exception(() => pipeline.Run(EventData.GenerateSampleEvent(TestConstants.EventId8)));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(2, organization.StackCount);
            Assert.Equal(3, organization.EventCount);
            Assert.Equal(3, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(2, project.StackCount);
            Assert.Equal(3, project.EventCount);
            Assert.Equal(3, project.TotalEventCount);

            _eventRepository.RemoveAllByStackIdAsync(ev.StackId).Wait();
            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(2, organization.StackCount);
            Assert.Equal(1, organization.EventCount);
            Assert.Equal(3, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(2, project.StackCount);
            Assert.Equal(1, project.EventCount);
            Assert.Equal(3, project.TotalEventCount);
        }

        [Fact]
        public void SyncEventStackTags() {
            const string Tag1 = "Tag One";
            const string Tag2 = "Tag Two";
            const string Tag2_Lowercase = "tag two";

            var ev = EventData.GenerateEvent(id: TestConstants.EventId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);
            ev.Tags = new TagSet { Tag1 };

            var pipeline = IoC.GetInstance<EventPipeline>();
            Assert.DoesNotThrow(() => pipeline.Run(ev));

            ev = _eventRepository.GetById(ev.Id);
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

        private void CreateData() {
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
                _organizationRepository.Save(organization);

                _projectRepository.Add(project);
            }
        }

        protected override void DisposeManagedResources() {
            _eventRepository.RemoveAll();
            _stackRepository.RemoveAll();
            _projectRepository.RemoveAll();
            _organizationRepository.RemoveAll();
        }
    }
}