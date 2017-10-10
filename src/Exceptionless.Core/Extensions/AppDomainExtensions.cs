using System;
using System.IO;

namespace Exceptionless.Core.Extensions {
    public static class AppDomainExtensions {
        public static void SetDataDirectory(this AppDomain appDomain) {
            string path = Path.GetFullPath(Path.Combine(appDomain.BaseDirectory, @"..\..\..\..\..\Exceptionless.Web\App_Data"));
            if (Directory.Exists(path))
                appDomain.SetData("DataDirectory", path);
        }
    }
}
