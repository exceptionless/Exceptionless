using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories.Configuration;

public static class EventTelemetryMapping
{
    public static PropertiesDescriptor<PersistentEvent> AddTelemetry(this PropertiesDescriptor<PersistentEvent> properties) => properties
        .Keyword(e => e.Outcome)
        .Keyword(e => e.Result)
        .Keyword(e => e.ParentReferenceId)
        .Keyword(e => e.RootReferenceId)
        .Flattened(e => e.Labels, p => p.DepthLimit(1))
        .Nested(e => e.Measurements, n => n.Dynamic(DynamicMapping.False).Properties(p => p
            .Keyword("name")
            .Keyword("unit")
            .DoubleNumber("value")));
}
