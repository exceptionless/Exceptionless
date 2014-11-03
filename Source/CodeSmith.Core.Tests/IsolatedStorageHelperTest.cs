using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Text;
using NUnit.Framework;
using CodeSmith.Core.Helpers;

namespace CodeSmith.Core.Tests
{
    [TestFixture]
    public class IsolatedStorageHelperTest
    {
        const string fileName = "test.config";
        const string TEST_VALUE = "abcdefghijklmnopqrstuvwxyz";

        [Test]
        public void CanStoreAndRetrieve()
        {
            TestItem toStore = new TestItem();
            TestItem toFind = null;

            toStore.Name = "Jeff Gonzalez";

            IsolatedStorageHelper<TestItem> helper = new IsolatedStorageHelper<TestItem>(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, fileName);
            helper.Store(toStore);

            toFind = helper.Retrieve();

            Assert.AreEqual(toStore.Name, toFind.Name);


        }
    }
}
