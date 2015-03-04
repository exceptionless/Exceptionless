using System;

namespace Exceptionless.Core.Models {
    public class EventResultBase {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }
        public bool Is404 { get; set; }
    }
}