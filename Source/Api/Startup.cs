using System;
using System.Threading.Tasks;
using Owin;

namespace Exceptionless.Api {
    public class Startup {
        public Task ConfigurationAsync(IAppBuilder builder) {
            return AppBuilder.BuildAsync(builder);
        }
    }
}