#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Tests {
    public abstract class DatabaseTestBase : DataTestBase {
        protected DatabaseTestBase(string databaseName, bool tearDownOnExit) : base(tearDownOnExit) {
            DatabaseName = databaseName;
        }

        protected override void ResetData() {
            if (!DatabaseExists())
                return;

            base.ResetData();
        }

        protected string DatabaseName { get; set; }

        protected abstract string ConnectionString();

        protected abstract bool DatabaseExists();
    }
}