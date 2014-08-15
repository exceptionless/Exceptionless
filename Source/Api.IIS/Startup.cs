using System;
using Owin;

namespace Exceptionless.Api.IIS {
    public class Startup {
        public void Configuration(IAppBuilder builder) {
            AppBuilder.Build(builder);
        }
    }
}