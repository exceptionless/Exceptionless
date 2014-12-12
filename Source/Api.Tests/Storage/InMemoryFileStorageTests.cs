using System;
using Exceptionless.Core.Storage;

namespace Exceptionless.Api.Tests.Storage {
    public class InMemoryFileStorageTests : FileStorageTestsBase {
        protected override IFileStorage GetStorage() {
            return new InMemoryFileStorage();
        }
    }
}
