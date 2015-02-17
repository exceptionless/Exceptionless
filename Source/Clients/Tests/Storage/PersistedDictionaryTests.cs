using System;
using System.Threading;
using Client.Tests.Utility;
using Exceptionless.Serializer;
using Exceptionless.Storage;
using Xunit;

namespace Client.Tests.Storage {
    public class PersistedDictionaryTests {
        [Fact]
        public void WillBeSaved() {
            var resetEvent = new AutoResetEvent(false);
            var storage = new InMemoryObjectStorage();
            var dict = new PersistedDictionary("test.json", storage, new DefaultJsonSerializer(), 10);
            dict.Saved += (sender, args) => resetEvent.Set();
            dict["test"] = "test";
            Assert.Equal("test", dict["test"]);
            bool success = resetEvent.WaitOne(250);
            Assert.True(success, "Failed to save dictionary.");
            Assert.True(storage.Exists("test.json"));
        }

        [Fact]
        public void WillSaveOnce() {
            var latch = new CountDownLatch(2);
            var storage = new InMemoryObjectStorage();
            var dict = new PersistedDictionary("test.json", storage, new DefaultJsonSerializer(), 50);
            dict.Saved += (sender, args) => latch.Signal();
            for (int i = 0; i < 10; i++)
                dict["test" + i] = i.ToString();
            Assert.Equal(10, dict.Count);
            bool success = latch.Wait(250);
            Assert.False(success, "Dictionary was saved multiple times.");
            Assert.Equal(1, latch.Remaining);
            Assert.True(storage.Exists("test.json"));

            dict["test"] = "test";
            Assert.Equal(11, dict.Count);
            success = latch.Wait(250);
            Assert.True(success, "Failed to save dictionary.");
            Assert.True(storage.Exists("test.json"));
        }
    }
}
