using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Plugins.EventParser {
    public abstract class EventMapperBase<T> where T : class {
        public PersistentEvent Map(T source) {
            var ev = CreateEvent(source);
            if (ev != null)
                MapKnownDataTypes(ev, source);

            return ev;
        }

        protected virtual PersistentEvent CreateEvent(T source) {
            return new PersistentEvent();
        }

        protected virtual void MapKnownDataTypes(Event ev, T source) {
            ev.SetError(MapError(source));
            ev.SetError(MapSimpleError(source));
            if (ev.Data.ContainsKey(Event.KnownDataKeys.Error) || ev.Data.ContainsKey(Event.KnownDataKeys.SimpleError))
                ev.Type = Event.KnownTypes.Error;

            ev.SetEnvironmentInfo(MapEnvironmentInfo(source));
            ev.SetLocation(MapLocation(source));
            ev.AddRequestInfo(MapRequestInfo(source));
            ev.SetSubmissionMethod(MapSubmissionMethod(source));
            ev.SetUserIdentity(MapUserInfo(source));
            ev.SetUserDescription(MapUserDescription(source));
            ev.SetVersion(MapVersion(source));
        }

        protected virtual Error MapError(T source) {
            return null;
        }
        
        protected virtual SimpleError MapSimpleError(T source) {
            return null;
        }
        
        protected virtual EnvironmentInfo MapEnvironmentInfo(T source) {
            return null;
        }

        protected virtual Location MapLocation(T source) {
            return null;
        }

        protected virtual RequestInfo MapRequestInfo(T source) {
            return null;
        }
        
        protected virtual string MapSubmissionMethod(T source) {
            return null;
        }

        protected virtual UserInfo MapUserInfo(T source) {
            return null;
        }

        protected virtual UserDescription MapUserDescription(T source) {
            return null;
        }

        protected virtual string MapVersion(T source) {
            return null;
        }
    }
}
