using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Dependency;
using CodeSmith.Core.Helpers;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Stacking {
    public class EventStacker {
        private static List<Type> _eventStackers;
        private readonly IDependencyResolver _dependencyResolver;

        public EventStacker(IDependencyResolver dependencyResolver = null) {
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
        }

        /// <summary>
        /// Runs all of the event stackers and returns a dictionary of signature info to be used for stacking.
        /// </summary>
        /// <param name="ev"></param>
        /// <returns></returns>
        public IDictionary<string, string> GetSignatureInfo(Event ev) {
            var signatureInfo = new Dictionary<string, string>();
            foreach (Type stackerType in GetEventStackerTypes()) {
                var eventStacker = _dependencyResolver.GetService(stackerType) as IEventStacker;
                if (eventStacker == null)
                    continue;

                try {
                    eventStacker.AddSignatureInfo(ev, signatureInfo);
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error running event stacker \"{0}\": {1}", stackerType, ex.Message).Write();
                }
            }

            return signatureInfo;
        }

        /// <summary>
        /// Finds all of the event stacker types from the loaded modules.
        /// </summary>
        /// <returns>An enumerable list of event stacker types in priority order.</returns>
        protected virtual IList<Type> GetEventStackerTypes() {
            if (_eventStackers == null)
                _eventStackers = TypeHelper.GetPrioritizedDerivedTypes<IEventStacker>().ToList();

            return _eventStackers;
        }
    }
}
