using System;
using System.IO;

namespace Exceptionless.Core.Extensions {
    public static class AppDomainExtensions {
        public static void SetDataDirectory(this AppDomain appDomain) {
            var path = Path.Combine(appDomain.BaseDirectory, @"..\..\..\..\Api\App_Data");
            if (Directory.Exists(path))
                appDomain.SetData("DataDirectory", path);
        }
    }
}
