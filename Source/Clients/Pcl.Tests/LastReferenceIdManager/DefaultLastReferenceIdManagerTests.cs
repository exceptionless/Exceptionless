using System;
using Exceptionless.Dependency;
using Xunit;

namespace Pcl.Tests.Enrichments {
    public class DefaultLastReferenceIdManagerTests {
        [Fact]
        public void VerfiyGetSetAndClear() {
            var lastReferenceIdManager = DependencyResolver.Default.GetLastReferenceIdManager();
            Assert.Null(lastReferenceIdManager.GetLast());

            var key = Guid.NewGuid().ToString();
            lastReferenceIdManager.SetLast(key);
            Assert.Equal(key, lastReferenceIdManager.GetLast());
            
            lastReferenceIdManager.ClearLast();
            Assert.Null(lastReferenceIdManager.GetLast());
        }
    }
}