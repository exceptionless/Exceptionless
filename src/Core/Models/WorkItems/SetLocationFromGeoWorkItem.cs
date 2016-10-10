using System;

namespace Exceptionless.Core.Models.WorkItems {
    public class SetLocationFromGeoWorkItem {
        public string EventId { get; set; }
        public string Geo { get; set; }
    }
}