using System;
using Exceptionless.Core;
using Exceptionless.Core.Storage;

namespace Exceptionless.Api.Tests.Storage {
    public class AzureStorageTests : FileStorageTestsBase {
        protected override IFileStorage GetStorage() {
            return new AzureFileStorage(Settings.Current.AzureStorageConnectionString);
        }
    }
}
