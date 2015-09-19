using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Microsoft.Owin;
using Owin;

namespace Exceptionless.Api.Extensions {
    public static class AppBuilderExtensions {
        public static IAppBuilder CreatePerContext<T>(this IAppBuilder app, Func<Task<T>> createCallback) where T : class {
            return CreatePerContext(app, null, context => createCallback());
        }

        public static IAppBuilder CreatePerContext<T>(this IAppBuilder app, string key, Func<Task<T>> createCallback) where T : class {
            return CreatePerContext(app, key, context => createCallback());
        }

        public static IAppBuilder CreatePerContext<T>(this IAppBuilder app, Func<IOwinContext, Task<T>> createCallback) where T : class {
            return CreatePerContext(app, null, createCallback);
        }

        public static IAppBuilder CreatePerContext<T>(this IAppBuilder app, string key, Func<IOwinContext, Task<T>> createCallback) where T : class {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (createCallback == null)
                throw new ArgumentNullException(nameof(createCallback));

            app.Use(typeof(FactoryMiddleware<T>), key, createCallback);

            return app;
        }
    }

    public class FactoryMiddleware<TResult> : OwinMiddleware where TResult : class {
        private readonly Func<IOwinContext, Task<TResult>> _createCallback;
        private readonly string _key;

        public FactoryMiddleware(OwinMiddleware next, string key, Func<IOwinContext, Task<TResult>> createCallback) : base(next) {
            _createCallback = createCallback;
            _key = key ?? "FactoryMiddleware" + typeof(TResult).AssemblyQualifiedName;
        }

        public override async Task Invoke(IOwinContext context) {
            TResult instance = await _createCallback(context).AnyContext();
            try {
                context.Set(_key, instance);
                if (Next != null)
                    await Next.Invoke(context).AnyContext();
            } finally {
                var disposable = instance as IDisposable;
                disposable?.Dispose();
            }
        }
    }
}
