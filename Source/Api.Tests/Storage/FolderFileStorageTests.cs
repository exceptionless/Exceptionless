using System;
using Exceptionless.Core.Storage;
using Xunit;

namespace Exceptionless.Api.Tests.Storage {
    public class FolderFileStorageTests : FileStorageTestsBase {
        private const string DATA_DIRECTORY_QUEUE_FOLDER = @"|DataDirectory|\Queue";

        protected override IFileStorage GetStorage() {
            return new FolderFileStorage("temp");
        }

        [Fact]
        public void CanUseDataDirectory() {
            var storage = new FolderFileStorage(DATA_DIRECTORY_QUEUE_FOLDER);
            Assert.NotNull(storage.Folder);
            Assert.NotEqual(DATA_DIRECTORY_QUEUE_FOLDER, storage.Folder);
            Assert.True(storage.Folder.EndsWith("Queue\\"), storage.Folder);
        }
    }
}
