using System;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class StackTrace {
        public int LineNumber { get; set; }

        public string ClassName { get; set; }

        public int ColumnNumber { get; set; }

        public string FileName { get; set; }

        public string MethodName { get; set; }
    }
}
