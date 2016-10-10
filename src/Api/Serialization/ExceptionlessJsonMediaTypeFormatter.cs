using System;
using System.Net.Http.Formatting;
using Exceptionless.Core.Serialization;
using Newtonsoft.Json;

namespace Exceptionless.Api.Serialization {
    public class ExceptionlessJsonMediaTypeFormatter : JsonMediaTypeFormatter {
        public ExceptionlessJsonMediaTypeFormatter() {
            SerializerSettings.Formatting = Formatting.Indented;
            SerializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore;
            SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            SerializerSettings.ContractResolver = new LowerCaseUnderscorePropertyNamesContractResolver();
        }
    }
}