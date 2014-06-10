using System;
using System.Collections.Generic;
using Exceptionless.Enrichments;

namespace Exceptionless {
    public static class ClientExtensions {
        public static void SubmitUnhandledException(this ExceptionlessClient client, Exception exception) {
            exception.ToExceptionless(client: client).Submit();
        }

        public static void SubmitException(this ExceptionlessClient client, Exception exception) {
            exception.ToExceptionless(client: client).Submit();
        }

        public static void SubmitLog(this ExceptionlessClient client, string message) {
            client.CreateEventBuilder().SetUserDescription(message).Submit();
        }
    }
}
