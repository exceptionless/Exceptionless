using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class PersistentEventMapping {
        public static PersistentEvent Map(RaygunModel model) {
            var ev = new PersistentEvent {
                Date = model.OccurredOn,
                Type = Event.KnownTypes.Error,
                
            };

            var details = model.Details;
            if (details != null) {
                ev.SetVersion(details.Version);

                if (details.Tags != null)
                    ev.Tags.AddRange(details.Tags);

                // TODO: Set the stacking key once https://github.com/exceptionless/Exceptionless/pull/187 is merged.
                //if (!String.IsNullOrEmpty(details.GroupingKey))
                //    ev.SetManualStackingKey(details.GroupingKey);

                if (details.UserCustomData != null)
                    foreach (var kvp in details.UserCustomData)
                        ev.Data[kvp.Key] = kvp.Value;

                if (details.Client != null)
                    ev.Data[nameof(details.Client)] = details.Client;

                if (details.Response != null)
                    ev.Data[nameof(details.Response)] = details.Response;

                if (!String.IsNullOrEmpty(details.Error?.Message))
                    ev.Message = details.Error.Message;
            }

            ev.AddRequestInfo(RequestInfoMapping.Map(model));
            ev.SetEnvironmentInfo(EnvironmentInfoMapping.Map(model));
            ev.SetUserIdentity(UserInfoMapping.Map(model));
            ev.SetError(ErrorMapping.Map(model));

            return ev;
        }
    }
}
