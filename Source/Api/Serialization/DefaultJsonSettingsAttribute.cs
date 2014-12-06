using System;
using System.Web.Http.Controllers;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Api.Serialization {
    public class DefaultJsonSettingsAttribute : Attribute, IControllerConfiguration {
        public void Initialize(HttpControllerSettings controllerSettings, HttpControllerDescriptor controllerDescriptor) {
            controllerSettings.Formatters.JsonFormatter.SerializerSettings.ContractResolver = new DefaultContractResolver();
        }
    }
}
