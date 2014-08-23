using System;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.Models.Admin;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class WebHookRepositoryTests : IDisposable {
        private readonly IWebHookRepository _repository = IoC.GetInstance<IWebHookRepository>();

        [Fact]
        public void GetGetByOrganizationIdOrProjectId() {
            RemoveData();

            _repository.Add(new WebHook { OrganizationId = TestConstants.OrganizationId, Url = "http://localhost:40000/test1", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted } });
            _repository.Add(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, Url = "http://localhost:40000/test", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted } });

            Assert.Equal(2, _repository.GetByOrganizationId(TestConstants.OrganizationId).Count);
            Assert.Equal(2, _repository.GetByOrganizationIdOrProjectId(TestConstants.OrganizationId, TestConstants.ProjectId).Count);
            Assert.Equal(1, _repository.GetByProjectId(TestConstants.ProjectId).Count);
        }

        protected void RemoveData() {
            _repository.RemoveAll();
        }

        public void Dispose() {
            //RemoveData();
        }
    }
}