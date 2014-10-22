using System;
using System.Net.Http.Formatting;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Api.Serialization {
    public class ExceptionlessJsonMediaTypeFormatter : JsonMediaTypeFormatter {
        public ExceptionlessJsonMediaTypeFormatter() {
            SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
            SerializerSettings.ContractResolver = new LowerCaseUnderscorePropertyNamesContractResolver();
        }
    }
}