using System;
using Exceptionless.Storage;
using Xunit;

namespace Client.Tests.Storage {
    public class KeyValueStorageTests {
        [Fact]
        public void CanManageKeys() {
            IKeyValueStorage storage = new InMemoryKeyValueStorage();
            storage.Set("group", "test", "test");
            Assert.Equal("test", storage.Get("group", "test"));
        }
    }
}
