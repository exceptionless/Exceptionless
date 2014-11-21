using System;

namespace Exceptionless.Models.Data {
    public class Error : InnerError {
        public Error() {
            Modules = new ModuleCollection();
        }

        /// <summary>
        /// Any modules that were loaded / referenced when the error occurred.
        /// </summary>
        public ModuleCollection Modules { get; set; }

        public static class KnownDataKeys {
            public const string ExtraProperties = "ext";
            public const string TargetInfo = "ti";
        }
    }
}
