using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;

namespace Exceptionless.Api.Extensions {
    public static class AppBuilderExtensions {
        public static IAppBuilder CreatePerContext<T>(this IAppBuilder app, Func<T> createCallback) where T : class {
            return CreatePerContext(app, null, context => createCallback());
        }

        public static IAppBuilder CreatePerContext<T>(this IAppBuilder app, string key, Func<T> createCallback) where T : class {
            return CreatePerContext(app, key, context => createCallback());
        }

        public static IAppBuilder CreatePerContext<T>(this IAppBuilder app, string key, Func<IOwinContext, T> createCallback) where T : class {
            if (app == null)
                throw new ArgumentNullException("app");
            if (createCallback == null)
                throw new ArgumentNullException("createCallback");

            app.Use(typeof(FactoryMiddleware<T>), key, createCallback);

            return app;
        }
    }

    public class FactoryMiddleware<TResult> : OwinMiddleware where TResult : class {
        private readonly Func<IOwinContext, TResult> _createCallback;
        private readonly string _key;

        public FactoryMiddleware(OwinMiddleware next, string key, Func<IOwinContext, TResult> createCallback) : base(next) {
            _createCallback = createCallback;
            _key = key ?? "FactoryMiddleware" + typeof(TResult).AssemblyQualifiedName;
        }

        public override async Task Invoke(IOwinContext context) {
            TResult instance = _createCallback(context);
            try {
                context.Set(_key, instance);
                if (Next != null)
                    await Next.Invoke(context);
            } finally {
                var disposable = instance as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
            }
        }
    }
}
