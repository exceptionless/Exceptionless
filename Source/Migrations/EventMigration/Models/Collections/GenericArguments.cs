using System;
using System.Collections.ObjectModel;

namespace Exceptionless.EventMigration.Models {
    public class GenericArguments : Collection<string> {
        public Exceptionless.Core.Models.GenericArguments ToGenericArguments() {
            var arguments = new Exceptionless.Core.Models.GenericArguments();
            foreach (var item in Items) {
                arguments.Add(item);
            }

            return arguments;
        }
    }
}