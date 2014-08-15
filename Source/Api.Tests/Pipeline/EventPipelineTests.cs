using System;
using System.Collections.Generic;
using System.IO;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Pipeline {
    public class EventPipelineTests : IDisposable {
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();
        private readonly BillingManager _billingManager = IoC.GetInstance<BillingManager>();

        public EventPipelineTests() {
            RemoveData(true);
            CreateData();
        }

        [Fact]
        public void VerifyOrganizationAndProjectStatistics() {
            RemoveData(true);
            CreateData();

            PersistentEvent ev = EventData.GenerateEvent(id: TestConstants.EventId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);

            var organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.NotNull(organization);
            Assert.Equal(0, organization.TotalEventCount);

            Assert.Equal(1, _projectRepository.GetCountByOrganizationId(organization.Id));
            var project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.NotNull(project);
            Assert.Equal(0, project.TotalEventCount);

            var pipeline = IoC.GetInstance<EventPipeline>();
            Exception exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.TotalEventCount);

            exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);
            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(1, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(1, project.TotalEventCount);

            ev.Id = TestConstants.EventId2;
            exception = Record.Exception(() => pipeline.Run(ev));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(2, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(2, project.TotalEventCount);

            exception = Record.Exception(() => pipeline.Run(EventData.GenerateSampleEvent(TestConstants.EventId8)));
            Assert.Null(exception);

            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(3, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(3, project.TotalEventCount);

            _eventRepository.RemoveAllByStackIdAsync(ev.StackId).Wait();
            organization = _organizationRepository.GetById(TestConstants.OrganizationId);
            Assert.Equal(3, organization.TotalEventCount);

            project = _projectRepository.GetById(TestConstants.ProjectId);
            Assert.Equal(3, project.TotalEventCount);
        }

        [Fact]
        public void SyncStackTags() {
            const string Tag1 = "Tag One";
            const string Tag2 = "Tag Two";
            const string Tag2_Lowercase = "tag two";

            PersistentEvent ev = EventData.GenerateEvent(id: TestConstants.EventId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, nestingLevel: 5, minimiumNestingLevel: 1);
            ev.Tags.Add(Tag1);

            var pipeline = IoC.GetInstance<EventPipeline>();
            Assert.DoesNotThrow(() => pipeline.Run(ev));

            ev = _eventRepository.GetById(ev.Id);
            Assert.NotNull(ev);
            Assert.NotNull(ev.StackId);

            var stack = _stackRepository.GetById(ev.StackId);
            Assert.Equal(new TagSet { Tag1 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, nestingLevel: 5, minimiumNestingLevel: 1);
            ev.Tags.Add(Tag2);

            Assert.DoesNotThrow(() => pipeline.Run(ev));
            stack = _stackRepository.GetById(ev.StackId);
            Assert.Equal(new TagSet { Tag1, Tag2 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, nestingLevel: 5, minimiumNestingLevel: 1);
            ev.Tags.Add(Tag2_Lowercase);

            Assert.DoesNotThrow(() => pipeline.Run(ev));
            stack = _stackRepository.GetById(ev.StackId);
            Assert.Equal(new TagSet { Tag1, Tag2 }, stack.Tags);
        }

        [Fact]
        public void EnsureSingleRegression() {
            var pipeline = IoC.GetInstance<EventPipeline>();

            PersistentEvent ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, nestingLevel: 5, minimiumNestingLevel: 1);
            var context = new EventContext(ev);
            Assert.DoesNotThrow(() => pipeline.Run(context));
            Assert.False(context.IsRegression);

            ev = _eventRepository.GetById(ev.Id);
            Assert.NotNull(ev);

            var stack = _stackRepository.GetById(ev.StackId);
            stack.DateFixed = DateTime.UtcNow;
            stack.IsRegressed = false;
            _stackRepository.Save(stack);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddDays(1), nestingLevel: 5, minimiumNestingLevel: 1);
            context = new EventContext(ev);
            Assert.DoesNotThrow(() => pipeline.Run(context));
            Assert.True(context.IsRegression);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddDays(1), nestingLevel: 5, minimiumNestingLevel: 1);
            context = new EventContext(ev);
            Assert.DoesNotThrow(() => pipeline.Run(context));
            Assert.False(context.IsRegression);
        }

        [Theory]
        [PropertyData("Events")]
        public void ProcessEvents(string errorFilePath) {
            // TODO: We currently fail to process this error due to https://jira.mongodb.org/browse/CSHARP-930
            if (errorFilePath.Contains("881"))
                return;
            
            var parserPluginManager = IoC.GetInstance<EventParserPluginManager>();
            var events = parserPluginManager.ParseEvents(File.ReadAllText(errorFilePath), 2, "exceptionless/2.0.0.0");
            Assert.NotNull(events);
            Assert.True(events.Count > 0);

            var pipeline = IoC.GetInstance<EventPipeline>();
            foreach (var ev in events) {
                ev.ProjectId = TestConstants.ProjectId;
                ev.OrganizationId = TestConstants.OrganizationId;

                Assert.DoesNotThrow(() => pipeline.Run(ev));
            }
        }

        public static IEnumerable<object[]> Events {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\ErrorData\", "*.expected.json", SearchOption.AllDirectories))
                    result.Add(new object[] { file });

                return result.ToArray();
            }
        }

        private void CreateData() {
            foreach (Organization organization in OrganizationData.GenerateSampleOrganizations()) {
                if (organization.Id == TestConstants.OrganizationId3)
                    _billingManager.ApplyBillingPlan(organization, BillingManager.FreePlan, UserData.GenerateSampleUser());
                else
                    _billingManager.ApplyBillingPlan(organization, BillingManager.SmallPlan, UserData.GenerateSampleUser());

                organization.StripeCustomerId = Guid.NewGuid().ToString("N");
                organization.CardLast4 = "1234";
                organization.SubscribeDate = DateTime.Now;

                if (organization.IsSuspended) {
                    organization.SuspendedByUserId = TestConstants.UserId;
                    organization.SuspensionCode = SuspensionCode.Billing;
                    organization.SuspensionDate = DateTime.Now;
                }

                _organizationRepository.Add(organization);
            }

            foreach (Project project in ProjectData.GenerateSampleProjects()) {
                var organization = _organizationRepository.GetById(project.OrganizationId);
                _organizationRepository.Save(organization);

                _projectRepository.Add(project);
            }

            foreach (User user in UserData.GenerateSampleUsers()) {
                if (user.Id == TestConstants.UserId) {
                    user.OrganizationIds.Add(TestConstants.OrganizationId2);
                    user.OrganizationIds.Add(TestConstants.OrganizationId3);
                }

                if (!user.IsEmailAddressVerified) {
                    user.VerifyEmailAddressToken = Guid.NewGuid().ToString();
                    user.VerifyEmailAddressTokenExpiration = DateTime.Now.AddDays(1);
                }
                _userRepository.Add(user);
            }
        }

        private void RemoveData(bool removeUserAndProjectAndOrganizationData = false) {
            _eventRepository.RemoveAll(false);
            _stackRepository.RemoveAll(false);

            if (!removeUserAndProjectAndOrganizationData)
                return;

            _userRepository.RemoveAll(false);
            _projectRepository.RemoveAll(false);
            _organizationRepository.RemoveAll(false);
        }

        public void Dispose() {
            RemoveData();
        }
    }
}