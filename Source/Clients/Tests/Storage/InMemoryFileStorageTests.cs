using System;
using Exceptionless.Storage;

namespace Client.Tests.Storage {
    public class InMemoryFileStorageTests : FileStorageTestsBase {
        protected override IFileStorage GetStorage() {
            return new InMemoryFileStorage();
        }
    }
}
