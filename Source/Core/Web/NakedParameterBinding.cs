using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Metadata;

namespace Exceptionless.Core.Web {
    public class NakedBodyParameterBinding : HttpParameterBinding {
        public NakedBodyParameterBinding(HttpParameterDescriptor descriptor) : base(descriptor) {}

        public override Task ExecuteBindingAsync(ModelMetadataProvider metadataProvider, HttpActionContext actionContext, CancellationToken cancellationToken) {
            var binding = actionContext.ActionDescriptor.ActionBinding;
            
            if (actionContext.Request.Method == HttpMethod.Get || binding.ParameterBindings.Count(b => b.Descriptor.ParameterBinderAttribute is NakedBodyAttribute) > 1)
                return EmptyTask.Start();

            var type = binding.ParameterBindings[0].Descriptor.ParameterType;

            if (type == typeof(string)) {
                return actionContext.Request.Content
                        .ReadAsStringAsync()
                        .ContinueWith(task => {
                            var stringResult = task.Result;
                            SetValue(actionContext, stringResult);
                        }, cancellationToken);
            }

            if (type == typeof(byte[])) {
                return actionContext.Request.Content
                    .ReadAsByteArrayAsync()
                    .ContinueWith(task => {
                        byte[] result = task.Result;
                        SetValue(actionContext, result);
                    }, cancellationToken);
            }

            throw new InvalidOperationException("Only string and byte[] are supported for [NakedBody] parameters");
        }

        public override bool WillReadBody { get { return true; } }
    }

    public class EmptyTask {
        public static Task Start() {
            var taskSource = new TaskCompletionSource<AsyncVoid>();
            taskSource.SetResult(default(AsyncVoid));
            return taskSource.Task;
        }

        private struct AsyncVoid {}
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class NakedBodyAttribute : ParameterBindingAttribute {
        public override HttpParameterBinding GetBinding(HttpParameterDescriptor parameter) {
            if (parameter == null)
                throw new ArgumentException("Invalid parameter");

            return new NakedBodyParameterBinding(parameter);
        }
    }
}
