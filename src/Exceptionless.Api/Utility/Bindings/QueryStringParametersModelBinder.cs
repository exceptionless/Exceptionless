using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Api.Utility {
    public class QueryStringParametersModelBinder : IModelBinder {
        private readonly SimpleTypeModelBinder _simpleModelBinder;

        public QueryStringParametersModelBinder(Type type) {
            _simpleModelBinder = new SimpleTypeModelBinder(type);
        }

        public Task BindModelAsync(ModelBindingContext bindingContext) {
            if (bindingContext == null)
                throw new ArgumentNullException(nameof(bindingContext));

            var parameter = bindingContext.ActionContext.ActionDescriptor.Parameters.FirstOrDefault(p => p.Name == bindingContext.FieldName) as ControllerParameterDescriptor;

            if (parameter == null)
                return _simpleModelBinder.BindModelAsync(bindingContext);

            if (parameter.ParameterInfo.GetCustomAttributes(typeof(IpAddressAttribute), false).Any()) {
                bindingContext.Result = ModelBindingResult.Success(bindingContext.HttpContext.Connection.RemoteIpAddress.ToString());
                return Task.CompletedTask;
            }

            if (parameter.ParameterInfo.GetCustomAttributes(typeof(UserAgentAttribute), false).Any()) {
                string userAgent = bindingContext.HttpContext.Request.Headers[HeaderNames.UserAgent].ToString();
                bindingContext.Result = ModelBindingResult.Success(userAgent);
                return Task.CompletedTask;
            }

            if (parameter.ParameterInfo.GetCustomAttributes(typeof(ReferrerAttribute), false).Any()) {
                string urlReferrer = bindingContext.HttpContext.Request.Headers[HeaderNames.Referer].ToString();
                bindingContext.Result = ModelBindingResult.Success(urlReferrer);
                return Task.CompletedTask;
            }

            if (parameter.ParameterInfo.GetCustomAttributes(typeof(QueryStringParametersAttribute), false).Any()) {
                var query = bindingContext.HttpContext.Request.Query;
                bindingContext.Result = ModelBindingResult.Success(query);
                return Task.CompletedTask;
            }

            return _simpleModelBinder.BindModelAsync(bindingContext);
        }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class QueryStringParametersAttribute : Attribute { }

    public class QueryStringParametersModelBinderProvider : IModelBinderProvider {
        public IModelBinder GetBinder(ModelBinderProviderContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Metadata.IsComplexType || context.Metadata.ModelType != typeof(string))
                return null;

            return new QueryStringParametersModelBinder(context.Metadata.ModelType);
        }
    }
}