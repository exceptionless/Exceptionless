using System;
using System.Collections.Generic;
using System.Linq;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public abstract class ElasticSearchIndexBase<T> : IElasticSearchIndex where T : class {
        public abstract string Name { get; }

        public virtual int Version { get { return 1; } }

        public virtual string VersionedName {
            get { return String.Concat(Name, "-v", Version); }
        }

        public virtual IDictionary<Type, string> GetIndexTypeNames() {
            return new Dictionary<Type, string> {
                { typeof(T), Name }
            };
        }

        public virtual IEnumerable<KeyValuePair<Type, string>> GetTypeIndices() {
            return GetIndexTypeNames().Select(kvp => new KeyValuePair<Type, string>(kvp.Key, VersionedName));
        }

        public virtual CreateIndexDescriptor CreateIndex(CreateIndexDescriptor idx) {
            idx.AddMapping<T>(CreateMapping);
            return idx;
        }

        protected virtual PutMappingDescriptor<T> CreateMapping(PutMappingDescriptor<T> map) {
            throw new NotImplementedException();
        }
    }
}