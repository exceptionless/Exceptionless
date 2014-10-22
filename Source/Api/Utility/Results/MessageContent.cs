using System;

namespace Exceptionless.Api.Utility.Results {
    internal class MessageContent {
        public MessageContent(string message) {
            Message = message;
        }

        public string Message { get; private set; }
    }
}