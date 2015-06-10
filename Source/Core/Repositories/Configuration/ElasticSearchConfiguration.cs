using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net.ConnectionPool;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ElasticSearchConfiguration {
        private readonly IQueue<WorkItemData> _workItemQueue;

        public ElasticSearchConfiguration(IQueue<WorkItemData> workItemQueue) {
            _workItemQueue = workItemQueue;
        }

        public IElasticClient GetClient(IEnumerable<Uri> serverUris) {
            var connectionPool = new StaticConnectionPool(serverUris);
            var indexes = GetIndexes();
            var settings = new ConnectionSettings(connectionPool)
                .MapDefaultTypeIndices(t => t.AddRange(indexes.ToTypeIndices()))
                .MapDefaultTypeNames(t => {
                    t.AddRange(indexes.SelectMany(idx => idx.GetIndexTypes()));
                })
                .SetDefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .SetDefaultPropertyNameInferrer(p => p.ToLowerUnderscoredWords());
            var client = new ElasticClient(settings, new KeepAliveHttpConnection(settings));
            ConfigureIndexes(client);
            return client;
        }

        public void ConfigureIndexes(IElasticClient client) {
            var indexes = GetIndexes();
            foreach (var index in indexes) {
                var idx = index;
                int currentVersion = GetAliasVersion(client, idx.Name);

                if (!client.IndexExists(idx.VersionedName).Exists)
                    client.CreateIndex(idx.VersionedName, idx.CreateIndex);

                if (!client.AliasExists(idx.Name).Exists)
                    client.Alias(a => a
                        .Add(add => add
                            .Index(idx.VersionedName)
                            .Alias(idx.Name)
                        )
                    );

                // already on current version
                if (currentVersion >= idx.Version || currentVersion < 1)
                    continue;

                // upgrade
                _workItemQueue.Enqueue(new ReindexWorkItem {
                    OldIndex = String.Concat(idx.Name, "-v", currentVersion),
                    NewIndex = idx.VersionedName,
                    Alias = idx.Name,
                    DeleteOld = true
                });
            }
        }

        public IEnumerable<IElasticSearchIndex> GetIndexes() {
            return new IElasticSearchIndex[] {
                new OrganizationIndex()
            };
        }

        public int GetAliasVersion(IElasticClient client, string alias) {
            var res = client.GetAlias(a => a.Alias(alias));
            if (!res.Indices.Any())
                return -1;

            string indexName = res.Indices.FirstOrDefault().Key;
            string[] parts = indexName.Split('-');
            if (parts.Length != 2)
                return -1;

            int version;
            if (!Int32.TryParse(parts[1].Substring(1), out version))
                return -1;

            return version;
        }
    }
}
