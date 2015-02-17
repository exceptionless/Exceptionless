using System;
using Exceptionless;
using Exceptionless.Dependency;
using Exceptionless.Extras.Storage;
using Exceptionless.Serializer;
using Exceptionless.Storage;

namespace Client.Tests.Storage {
    public class IsolatedStorageFileStorageTests : FileStorageTestsBase {
        protected override IObjectStorage GetStorage() {
            var resolver = new DefaultDependencyResolver();
            resolver.Register<IJsonSerializer, DefaultJsonSerializer>();
            return new IsolatedStorageObjectStorage(resolver);
        }
    }
}
