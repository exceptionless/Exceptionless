using System;
using System.Collections.Generic;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public interface IElasticSearchIndex {
        int Version { get; }
        string Name { get; }
        string VersionedName { get; }
        IDictionary<Type, string> GetIndexTypeNames();
        CreateIndexDescriptor CreateIndex(CreateIndexDescriptor idx);
    }

    public interface ITemplatedElasticSeachIndex : IElasticSearchIndex {
        PutTemplateDescriptor CreateTemplate(PutTemplateDescriptor template);
    }
}
