using System;
using System.Collections.ObjectModel;

namespace Exceptionless.EventMigration.Models {
    public class ParameterCollection : Collection<Parameter> {
        public Exceptionless.Core.Models.ParameterCollection ToParameters() {
            var parameters = new Exceptionless.Core.Models.ParameterCollection();
            foreach (var item in Items) {
                parameters.Add(item.ToParameter());
            }

            return parameters;
        }
    }
}