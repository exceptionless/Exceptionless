using System;
using Exceptionless;
using Exceptionless.Dependency;
using Exceptionless.Extras.Storage;
using Exceptionless.Serializer;
using Exceptionless.Storage;
using Xunit;

namespace Client.Tests.Storage {
    public class FolderFileStorageTests : FileStorageTestsBase {
        private const string DATA_DIRECTORY_QUEUE_FOLDER = @"|DataDirectory|\Queue";

        protected override IObjectStorage GetStorage() {
            var resolver = new DefaultDependencyResolver();
            resolver.Register<IJsonSerializer, DefaultJsonSerializer>();
            return new FolderObjectStorage(resolver, "temp");
        }

        [Fact]
        public void CanUseDataDirectory() {
            var resolver = new DefaultDependencyResolver();
            resolver.Register<IJsonSerializer, DefaultJsonSerializer>();

            var storage = new FolderObjectStorage(resolver, DATA_DIRECTORY_QUEUE_FOLDER);
            Assert.NotNull(storage.Folder);
            Assert.NotEqual(DATA_DIRECTORY_QUEUE_FOLDER, storage.Folder);
            Assert.True(storage.Folder.EndsWith("Queue\\"), storage.Folder);
        }
    }
}
