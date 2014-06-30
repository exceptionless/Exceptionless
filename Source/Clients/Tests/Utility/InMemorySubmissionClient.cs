using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Submission;

namespace Client.Tests.Utility {
    public class InMemorySubmissionClient : ISubmissionClient {
        public InMemorySubmissionClient() {
            Events = new List<Event>();
        }

        public List<Event> Events { get; private set; } 

        public SubmissionResponse PostEvents(IEnumerable<Event> events, ExceptionlessConfiguration config, IJsonSerializer serializer) {
            var data = events.ToList();
            data.ForEach(e => {
                if (e.Date == DateTimeOffset.MinValue)
                    e.Date = DateTimeOffset.Now;

                if (String.IsNullOrEmpty(e.Type))
                    e.Type = Event.KnownTypes.Log;
            });
            Events.AddRange(data);

            return new SubmissionResponse(202, "Accepted");
        }

        public SubmissionResponse PostUserDescription(string referenceId, UserDescription description, ExceptionlessConfiguration config, IJsonSerializer serializer) {
            var ev = Events.FirstOrDefault(e => e.ReferenceId == referenceId);
            if (ev == null)
                return new SubmissionResponse(404, "Not Found");

            ev.Data[Event.KnownDataKeys.UserDescription] = description;

            return new SubmissionResponse(200, "OK");
        }

        public SettingsResponse GetSettings(ExceptionlessConfiguration config, IJsonSerializer serializer) {
            return new SettingsResponse(true);
        }
    }
}
