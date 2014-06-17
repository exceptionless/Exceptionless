using System;
using Exceptionless.Extras.Storage;
using Exceptionless.Storage;

namespace Client.Tests.Storage {
    public class IsolatedStorageFileStorageTests : FileStorageTestsBase {
        protected override IFileStorage GetStorage() {
            return new IsolatedStorageFileStorage();
        }
    }
}
