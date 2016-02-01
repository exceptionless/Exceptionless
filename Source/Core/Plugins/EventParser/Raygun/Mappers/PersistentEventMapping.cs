using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class PersistentEventMapping {
        public static PersistentEvent Map(RaygunModel raygunModel) {
            var persistentEvent = new PersistentEvent();

            persistentEvent.Date = raygunModel.OccurredOn;
            persistentEvent.Type = Event.KnownTypes.Error;

            persistentEvent.SetError(ErrorMapping.Map(raygunModel));
            persistentEvent.SetEnvironmentInfo(EnvironmentInfoMapping.Map(raygunModel));
            persistentEvent.AddRequestInfo(RequestInfoMapping.Map(raygunModel));

            return persistentEvent;
        }
    }
}
