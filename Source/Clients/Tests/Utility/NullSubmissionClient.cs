using System;
using System.Collections.Generic;
using Exceptionless;
using Exceptionless.Models;
using Exceptionless.Submission;

namespace Client.Tests.Utility {
    public class NullSubmissionClient : ISubmissionClient {
        public SubmissionResponse Submit(IEnumerable<Event> events, ExceptionlessConfiguration config, IJsonSerializer serializer) {
            return new SubmissionResponse(202, "Accepted");
        }

        public SettingsResponse GetSettings(ExceptionlessConfiguration configuration, IJsonSerializer serializer) {
            return new SettingsResponse(true);
        }
    }
}
