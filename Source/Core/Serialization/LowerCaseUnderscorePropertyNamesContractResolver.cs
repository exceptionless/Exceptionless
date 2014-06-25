using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Core.Serialization {
    public class LowerCaseUnderscorePropertyNamesContractResolver : DefaultContractResolver {
        public LowerCaseUnderscorePropertyNamesContractResolver() : base(true) {}

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType) {
            if (objectType != typeof(DataDictionary) && objectType != typeof(SettingsDictionary))
                return base.CreateDictionaryContract(objectType);

            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
            contract.PropertyNameResolver = propertyName => propertyName;
            return contract;
        }

        protected override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }
}