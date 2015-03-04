using System;

namespace Exceptionless.Core.Models.Data {
    public class StackFrame : Method {
        public string FileName { get; set; }
        public int LineNumber { get; set; }
        public int Column { get; set; }
    }
}