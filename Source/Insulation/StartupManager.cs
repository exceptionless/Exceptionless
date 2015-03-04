using System;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation {
    public class StartupManager : IStartupManager {
        public void Startup(object value = null) {
            ExceptionlessClient.Default.Configuration.UseInMemoryStorage();
            ExceptionlessClient.Default.Configuration.UseReferenceIds();
            ExceptionlessClient.Default.Configuration.SetVersion(Settings.Current.Version);

            var config = value as HttpConfiguration;
            if (config != null)
                ExceptionlessClient.Default.RegisterWebApi(config);
            else
                ExceptionlessClient.Default.Register();
        }
    }
}