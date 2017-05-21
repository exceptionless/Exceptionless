using System;

namespace Exceptionless.Core.Models.Exceptions {
    public class WebHookException : Exception {
        public WebHookException(string message, Exception inner = null) : base(message, inner) { }

        public int StatusCode { get; set; }
        public bool Unauthorized { get; set; }
    }
}