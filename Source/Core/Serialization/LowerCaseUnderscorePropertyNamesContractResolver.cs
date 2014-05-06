using System;
using Exceptionless.Core.Extensions;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Core.Serialization {
    public class LowerCaseUnderscorePropertyNamesContractResolver : DefaultContractResolver {
        public LowerCaseUnderscorePropertyNamesContractResolver() : base(true) {}

        protected override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }
}