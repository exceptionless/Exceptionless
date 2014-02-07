#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models;
using Xunit;

namespace Exceptionless.Tests.Models {
    public class TagCollectionTests {
        [Fact]
        public void AddBasic() {
            var list = new TagSet();

            using (IDisposable tag = list.Add("Order"))
                Assert.Equal(1, list.Count);

            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void AddRemove() {
            var list = new TagSet();

            using (list.Add("Order")) {
                Assert.Equal(1, list.Count);

                list.Remove("Order");
            }

            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void AddRemoveNested() {
            var tags = new TagSet();

            using (tags.Add("Order")) {
                using (tags.Add("Step1")) {
                    Assert.Equal(2, tags.Count);
                    using (tags.Add("Step2"))
                        Assert.Equal(3, tags.Count);

                    Assert.Equal(2, tags.Count);
                }
            }

            Assert.Equal(0, tags.Count);
        }
    }
}