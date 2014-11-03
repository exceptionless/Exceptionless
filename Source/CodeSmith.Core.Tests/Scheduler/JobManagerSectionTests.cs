using System;
using System.Configuration;
using CodeSmith.Core.Scheduler;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Scheduler {
    [TestFixture]
    public class JobManagerSectionTests {
        [Test]
        public void Load() {
            var jobManager = ConfigurationManager.GetSection("jobManager") as JobManagerSection;

            Assert.IsNotNull(jobManager);
        }
    }
}