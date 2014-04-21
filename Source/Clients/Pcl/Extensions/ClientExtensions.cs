using System;

namespace Exceptionless {
    public static class ClientExtensions {
        public static void SubmitUnhandledException(this ExceptionlessClient client) { }
        public static void SubmitException(this ExceptionlessClient client) { }
        public static void SubmitLog(this ExceptionlessClient client, string message) { }
    }
}
