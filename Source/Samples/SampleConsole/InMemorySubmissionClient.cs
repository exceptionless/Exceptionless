using System;
using System.Collections.Generic;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Submission;

namespace Exceptionless.SampleConsole
{
    public class InMemorySubmissionClient: ISubmissionClient {
        private static readonly Dictionary<string, object> _eventRepository = new Dictionary<string, object>();
        private static readonly Dictionary<string, object> _userDescriptionRepository = new Dictionary<string, object>();

        public SubmissionResponse PostEvents(IEnumerable<Event> events, ExceptionlessConfiguration config, IJsonSerializer serializer) {
            foreach (Event e in events)
            {
                string data = serializer.Serialize(e);
                string referenceId = !string.IsNullOrWhiteSpace(e.ReferenceId)
                    ? e.ReferenceId
                    : Guid.NewGuid().ToString("D");
                _eventRepository[referenceId] = data;
            }
            return new SubmissionResponse(200);

        }

        public SubmissionResponse PostUserDescription(string referenceId, UserDescription description, ExceptionlessConfiguration config, IJsonSerializer serializer) {
            string data = serializer.Serialize(description);
            _userDescriptionRepository[referenceId] = data;
            return new SubmissionResponse(200);
        }

        public SettingsResponse GetSettings(ExceptionlessConfiguration config, IJsonSerializer serializer) {
            return new SettingsResponse(true);
        }
    }
}
