#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core;
using Exceptionless.Membership;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Import {
    public class ImportSampleDataTests : MongoRepositoryTestBaseWithIdentity<Project, IProjectRepository> {
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();

        public ImportSampleDataTests() : base(IoC.GetInstance<IProjectRepository>(), false) {}

        [Fact]
        public void ImportProjectData() {
            Assert.NotNull(Repository.GetById(TestConstants.ProjectId));
        }

        [Fact]
        public void ImportOrganizationData() {
            Assert.NotNull(_organizationRepository.GetById(TestConstants.OrganizationId));
        }

        [Fact]
        public void ImportUserData() {
            Assert.NotNull(_userRepository.GetById(TestConstants.UserId));
        }

        protected override void CreateData() {
            Repository.Add(ProjectData.GenerateSampleProject());
            _organizationRepository.Add(OrganizationData.GenerateSampleOrganizations());

            var membershipProvider = new MembershipProvider(_userRepository.Collection);
            foreach (User user in UserData.GenerateSampleUsers())
                membershipProvider.CreateAccount(user);
        }

        protected override void RemoveData() {
            base.RemoveData();
            _organizationRepository.DeleteAll();
            _userRepository.DeleteAll();
        }
    }
}