#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Tests.Utility;
using ServiceStack.CacheAccess;

namespace Exceptionless.Tests {
    public abstract class DataTestBase : TestBase {
        protected DataTestBase(bool tearDownOnExit) : base(tearDownOnExit) {}

        protected abstract void CreateData();

        protected abstract void RemoveData();

        protected virtual void ResetData() {
            RemoveData();
            ClearCache();
            CreateData();
        }

        protected void ClearCache() {
            var cacheClient = IoC.GetInstance<ICacheClient>();
            cacheClient.FlushAll();
        }

        private bool _dataCreated = false;

        protected override void SetUp() {
            ClearCache();
            if (!_dataCreated) {
                _dataCreated = true;
                CreateData();
            }
        }

        protected override void TearDown() {
            RemoveData();
        }
    }
}