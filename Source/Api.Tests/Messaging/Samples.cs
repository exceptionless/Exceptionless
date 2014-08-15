using System;

namespace Exceptionless.Api.Tests.Messaging {
    public class SimpleMessageA : ISimpleMessage {
        public string Data { get; set; }
    }

    public class SimpleMessageB : ISimpleMessage {
        public string Data { get; set; }
    }

    public class SimpleMessageC {
        public string Data { get; set; }
    }

    public interface ISimpleMessage {
        string Data { get; set; }
    }
}
