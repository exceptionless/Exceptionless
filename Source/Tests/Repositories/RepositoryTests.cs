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
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Repositories {
    public class RepositoryTests : MongoRepositoryTestBaseWithIdentity<Error, IErrorRepository> {
        public RepositoryTests() : base(IoC.GetInstance<IErrorRepository>(), true) {}

        [Fact]
        public void CreateUpdateRemove() {
            Assert.Equal(0, Repository.Count());

            Error entity = ErrorData.GenerateError();
            Assert.Null(entity.Id);

            // Insert a document
            var error = Repository.Add(entity);
            Assert.False(String.IsNullOrEmpty(error.Id));
            Assert.False(String.IsNullOrEmpty(entity.Id));
            Assert.Equal(error.Id, entity.Id);

            string id = error.Id;
            Assert.False(String.IsNullOrEmpty(id));

            // Find an existing document
            error = Repository.FirstOrDefault(e => e.Id == id);
            Assert.NotNull(error);
            Assert.Equal(id, error.Id);

            // Save a changed document
            error.UserDescription = "Unit Test 2";
            Repository.Update(error);

            // Update an existing document
            var errors = Repository.Where(e => e.Id == id);
            foreach (var er in errors)
                er.UserDescription = "Unit Test";

            Repository.Update(errors);

            Repository.Delete(id);
        }
    }
}