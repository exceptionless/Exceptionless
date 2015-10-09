using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Elasticsearch.Net.ConnectionPool;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Queues;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class ElasticSearchConfiguration {
        private readonly ThrottlingLockProvider _lockProvider;
        private readonly IQueue<WorkItemData> _workItemQueue;

        public ElasticSearchConfiguration(ICacheClient cacheClient, IQueue<WorkItemData> workItemQueue) {
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(1));
            _workItemQueue = workItemQueue;
        }

        public IElasticClient GetClient(IEnumerable<Uri> serverUris) {
            var connectionPool = new StaticConnectionPool(serverUris);
            var indexes = GetIndexes();
            var settings = new ConnectionSettings(connectionPool)
                .MapDefaultTypeIndices(t => t.AddRange(indexes.GetTypeIndices()))
                .MapDefaultTypeNames(t => t.AddRange(indexes.GetIndexTypeNames()))
                .SetDefaultTypeNameInferrer(p => p.Name.ToLowerUnderscoredWords())
                .SetDefaultPropertyNameInferrer(p => p.ToLowerUnderscoredWords())
                .MaximumRetries(5)
                .EnableMetrics();

            settings.SetJsonSerializerSettingsModifier(s => {
                s.ContractResolver = new EmptyCollectionElasticContractResolver(settings);
                s.AddModelConverters();
            });

            var client = new ElasticClient(settings, new KeepAliveHttpConnection(settings));
            ConfigureIndexes(client);
            return client;
        }

        public void ConfigureIndexes(IElasticClient client) {
            foreach (var index in GetIndexes()) {
                IIndicesOperationResponse response = null;
                int currentVersion = GetAliasVersion(client, index.Name);

                var templatedIndex = index as ITemplatedElasticSeachIndex;
                if (templatedIndex != null)
                    response = client.PutTemplate(index.VersionedName, template => templatedIndex.CreateTemplate(template).AddAlias(index.Name));
                else if (!client.IndexExists(index.VersionedName).Exists)
                    response = client.CreateIndex(index.VersionedName, descriptor => index.CreateIndex(descriptor).AddAlias(index.Name));

                Debug.Assert(response == null || response.IsValid, response?.ServerError != null ? response.ServerError.Error : "An error occurred creating the index or template.");

                // Add existing indexes to the alias.
                if (!client.AliasExists(index.Name).Exists) {
                    if (templatedIndex != null) {
                        var indices = client.IndicesStats().Indices.Where(kvp => kvp.Key.StartsWith(index.VersionedName)).Select(kvp => kvp.Key).ToList();
                        if (indices.Count > 0) {
                            var descriptor = new AliasDescriptor();
                            foreach (string name in indices)
                                descriptor.Add(add => add.Index(name).Alias(index.Name));

                            response = client.Alias(descriptor);
                        }
                    } else {
                        response = client.Alias(a => a.Add(add => add.Index(index.VersionedName).Alias(index.Name)));
                    }

                    Debug.Assert(response != null && response.IsValid, response?.ServerError != null ? response.ServerError.Error : "An error occurred creating the alias.");
                }

                // already on current version
                if (currentVersion >= index.Version || currentVersion < 1)
                    continue;

                // upgrade
                _lockProvider.TryUsingAsync("reindex", async () => {
                    await _workItemQueue.EnqueueAsync(new ReindexWorkItem {
                        OldIndex = String.Concat(index.Name, "-v", currentVersion),
                        NewIndex = index.VersionedName,
                        Alias = index.Name,
                        DeleteOld = true
                    });
                }, TimeSpan.Zero, CancellationToken.None);
            }
        }

        public void DeleteIndexes(IElasticClient client) {
            var deleteResponse = client.DeleteIndex(i => i.AllIndices());
            Debug.Assert(deleteResponse.IsValid, deleteResponse.ServerError != null ? deleteResponse.ServerError.Error : "An error occurred deleting the indexes.");

            foreach (var index in GetIndexes()) {
                var templatedIndex = index as ITemplatedElasticSeachIndex;
                if (templatedIndex != null) {
                    if (client.TemplateExists(index.VersionedName).Exists) {
                        var response = client.DeleteTemplate(index.VersionedName);
                        Debug.Assert(response.IsValid, response.ServerError != null ? response.ServerError.Error : "An error occurred deleting the index template.");
                    }
                }
            }
        }

        private IEnumerable<IElasticSearchIndex> GetIndexes() {
            return new IElasticSearchIndex[] {
                new StackIndex(),
                new EventIndex(),
                new OrganizationIndex()
            };
        }

        private int GetAliasVersion(IElasticClient client, string alias) {
            var res = client.GetAlias(a => a.Alias(alias));
            if (!res.Indices.Any())
                return -1;

            string indexName = res.Indices.FirstOrDefault().Key;
            string[] parts = indexName.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return -1;

            int version;
            if (!Int32.TryParse(parts[1].Substring(1), out version))
                return -1;

            return version;
        }
    }
}
