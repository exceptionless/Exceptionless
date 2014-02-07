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
using MongoDB.Driver;

namespace Exceptionless.Tests {
    public abstract class MongoRepositoryTestBase<TModel, TRepository> : DatabaseTestBase
        where TModel : class, new()
        where TRepository : class, IRepository<TModel> {
        private readonly TRepository _repository;

        protected MongoRepositoryTestBase(bool tearDownOnExit) : base(null, tearDownOnExit) {
            _repository = IoC.GetInstance<TRepository>();
            Reset();
        }

        protected MongoRepositoryTestBase(TRepository repository, bool tearDownOnExit) : base(null, tearDownOnExit) {
            _repository = repository;

            Reset();
        }

        public static MongoDatabase GetDatabase() {
            return IoC.GetInstance<MongoDatabase>();
        }

        protected override string ConnectionString() {
            return null;
        }

        protected override bool DatabaseExists() {
            return ReadOnlyRepository != null && ReadOnlyRepository.Collection.Exists();
        }

        protected override void CreateData() {}

        protected override void RemoveData() {
            _repository.DeleteAll();
        }

        private MongoReadOnlyRepository<TModel> ReadOnlyRepository { get { return _repository as MongoReadOnlyRepository<TModel>; } }

        protected TRepository Repository { get { return _repository; } }
    }

    public abstract class MongoRepositoryTestBaseWithIdentity<TModel, TRepository> : MongoRepositoryTestBase<TModel, TRepository>
        where TModel : class, IIdentity, new()
        where TRepository : class, IRepositoryWithIdentity<TModel> {
        protected MongoRepositoryTestBaseWithIdentity(bool tearDownOnExit) : base(IoC.GetInstance<TRepository>(), tearDownOnExit) {}

        protected MongoRepositoryTestBaseWithIdentity(TRepository repository, bool tearDownOnExit) : base(repository, tearDownOnExit) {}

        protected new TRepository Repository { get { return base.Repository as TRepository; } }
    }
}