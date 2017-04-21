using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models {
    public class MailMessageData {
        public string Subject { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }
}