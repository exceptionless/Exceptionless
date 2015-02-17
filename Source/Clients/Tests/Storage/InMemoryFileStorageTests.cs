using System;
using Exceptionless.Storage;

namespace Client.Tests.Storage {
    public class InMemoryFileStorageTests : FileStorageTestsBase {
        protected override IObjectStorage GetStorage() {
            return new InMemoryObjectStorage();
        }
    }
}
