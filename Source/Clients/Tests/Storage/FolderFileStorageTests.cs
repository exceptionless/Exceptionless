using System;
using Exceptionless.Extras.Storage;
using Exceptionless.Storage;

namespace Client.Tests.Storage {
    public class FolderFileStorageTests : FileStorageTestsBase {
        protected override IFileStorage GetStorage() {
            return new FolderFileStorage("temp");
        }
    }
}
