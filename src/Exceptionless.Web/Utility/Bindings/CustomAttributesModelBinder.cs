using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Utility {
    public class CustomAttributesModelBinder : IModelBinder {
        private readonly SimpleTypeModelBinder _simpleModelBinder;

        public CustomAttributesModelBinder(Type type, ILoggerFactory loggerFactory) {
            _simpleModelBinder = new SimpleTypeModelBinder(type, loggerFactory);
        }

        public Task BindModelAsync(ModelBindingContext bindingContext) {
            if (bindingContext == null)
                throw new ArgumentNullException(nameof(bindingContext));

            if (!(bindingContext.ActionContext.ActionDescriptor.Parameters.FirstOrDefault(p => p.Name == bindingContext.FieldName) is ControllerParameterDescriptor parameter))
                return _simpleModelBinder.BindModelAsync(bindingContext);

            if (bindingContext.ModelType == typeof(string)) {
                if (parameter.ParameterInfo.GetCustomAttributes(typeof(IpAddressAttribute), false).Any()) {
                    bindingContext.Result = ModelBindingResult.Success(bindingContext.HttpContext.Connection.RemoteIpAddress.ToString());
                    return Task.CompletedTask;
                }

                if (parameter.ParameterInfo.GetCustomAttributes(typeof(ContentTypeAttribute), false).Any()) {
                    string contentType = bindingContext.HttpContext.Request.Headers[HeaderNames.ContentType].ToString();
                    bindingContext.Result = ModelBindingResult.Success(contentType);
                    return Task.CompletedTask;
                }

                if (parameter.ParameterInfo.GetCustomAttributes(typeof(UserAgentAttribute), false).Any()) {
                    string userAgent;
                    if (bindingContext.HttpContext.Request.Headers.TryGetValue(Headers.Client, out var values) && values.Count > 0)
                        userAgent = values;
                    else
                        userAgent = bindingContext.HttpContext.Request.Headers[HeaderNames.UserAgent].ToString();
                    bindingContext.Result = ModelBindingResult.Success(userAgent);
                    return Task.CompletedTask;
                }

                if (parameter.ParameterInfo.GetCustomAttributes(typeof(ReferrerAttribute), false).Any()) {
                    string urlReferrer = bindingContext.HttpContext.Request.Headers[HeaderNames.Referer].ToString();
                    bindingContext.Result = ModelBindingResult.Success(urlReferrer);
                    return Task.CompletedTask;
                }
            } else {
                if (parameter.ParameterInfo.GetCustomAttributes(typeof(QueryStringParametersAttribute), false).Any()) {
                    var query = bindingContext.HttpContext.Request.Query;
                    bindingContext.Result = ModelBindingResult.Success(query);
                    return Task.CompletedTask;
                }
            }

            return _simpleModelBinder.BindModelAsync(bindingContext);
        }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class IpAddressAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class UserAgentAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ReferrerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class QueryStringParametersAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ContentTypeAttribute : Attribute { }

    public class CustomAttributesModelBinderProvider : IModelBinderProvider {
        public IModelBinder GetBinder(ModelBinderProviderContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Metadata.ModelType == typeof(string) || context.Metadata.ModelType == typeof(IQueryCollection))
                return new CustomAttributesModelBinder(context.Metadata.ModelType, context.Services.GetService<ILoggerFactory>());

            return null;
        }
    }
}