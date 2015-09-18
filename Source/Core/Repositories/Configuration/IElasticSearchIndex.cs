using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public interface IElasticSearchIndex {
        int Version { get; }
        string Name { get; }
        string VersionedName { get; }
        IDictionary<Type, string> GetIndexTypeNames();
        Task<CreateIndexDescriptor> CreateIndexAsync(CreateIndexDescriptor idx);
    }

    public interface ITemplatedElasticSeachIndex : IElasticSearchIndex {
        Task<PutTemplateDescriptor> CreateTemplateAsync(PutTemplateDescriptor template);
    }
}
