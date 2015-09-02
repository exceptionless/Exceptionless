using System;
using Owin;

namespace Exceptionless.Api {
    public class Startup {
        public void Configuration(IAppBuilder builder) {
            AppBuilder.Build(builder);
        }
    }
}