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
    // TODO: Ensure that every unit test is torn for each test: http://xunit.codeplex.com/wikipage?title=Comparisons
    public abstract class TestBase : IDisposable {
        protected TestBase(bool tearDownOnExit) {
            TearDownOnExit = tearDownOnExit;
        }

        protected abstract void SetUp();

        protected abstract void TearDown();

        protected void Reset() {
            TearDown();
            SetUp();
        }

        private bool TearDownOnExit { get; set; }

        public void Dispose() {
            if (TearDownOnExit)
                TearDown();
        }
    }
}