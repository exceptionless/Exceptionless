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
using Exceptionless.Core;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using MongoDB.Driver;
using Xunit;

namespace Exceptionless.Tests.Repositories {
    public class ProjectRepositoryTests : MongoRepositoryTestBaseWithIdentity<Project, IProjectRepository> {
        public ProjectRepositoryTests() : base(IoC.GetInstance<IProjectRepository>(), true) {}

        [Fact]
        public void CanFindByApiKey() {
            Repository.Add(ProjectData.GenerateProject(apiKeys: new List<string> {
                TestConstants.ApiKey2
            }, name: "Test"));
            Project p = Repository.GetByApiKey(TestConstants.ApiKey2);
            Assert.NotNull(p);
            Assert.Equal("Test", p.Name);
        }

        [Fact]
        public void DoNotAllowDuplicateApiKeys() {
            Repository.Add(ProjectData.GenerateProject(apiKeys: new List<string> {
                TestConstants.ApiKey3
            }, name: "Test"));

            var exception = Record.Exception(() => Repository.Add(ProjectData.GenerateProject(apiKeys: new List<string> {
                TestConstants.ApiKey3
            }, name: "Test")));
            Console.WriteLine(exception);

            Assert.NotNull(exception);
            Assert.IsType<MongoDuplicateKeyException>(exception);
        }

        [Fact]
        public void AllowEmptyApiKeys() {
            Project project = ProjectData.GenerateProject(name: "NoAPIKey1");
            project.ApiKeys.Clear();

            Project project2 = ProjectData.GenerateProject(name: "NoAPIKey2");
            project2.ApiKeys.Clear();

            Exception e = Record.Exception(() => {
                Repository.Add(project);
                Repository.Add(project2);
            });

            Assert.Null(e);
        }
    }
}