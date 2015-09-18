using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net.ConnectionPool;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Serialization;
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

        public async Task<IElasticClient> GetClientAsync(IEnumerable<Uri> serverUris) {
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
            await ConfigureIndexesAsync(client).AnyContext();
            return client;
        }

        public async Task ConfigureIndexesAsync(IElasticClient client) {
            foreach (var index in GetIndexes()) {
                IIndicesOperationResponse response = null;
                int currentVersion = await GetAliasVersionAsync(client, index.Name).AnyContext();
                
                var templatedIndex = index as ITemplatedElasticSeachIndex;
                if (templatedIndex != null)
                    response = await client.PutTemplateAsync(index.VersionedName, template => templatedIndex.CreateTemplate(template).AddAlias(index.Name)).AnyContext();
                else if ((await client.IndexExistsAsync(index.VersionedName).AnyContext()).Exists == false)
                    response = await client.CreateIndexAsync(index.VersionedName, descriptor => index.CreateIndex(descriptor).AddAlias(index.Name)).AnyContext();
                
                Debug.Assert(response == null || response.IsValid, response != null && response.ServerError != null ? response.ServerError.Error : "An error occurred creating the index or template.");

                // Add existing indexes to the alias.
                if ((await client.AliasExistsAsync(index.Name).AnyContext()).Exists == false) {
                    if (templatedIndex != null) {
                        var indices = (await client.IndicesStatsAsync().AnyContext()).Indices.Where(kvp => kvp.Key.StartsWith(index.VersionedName)).Select(kvp => kvp.Key).ToList();
                        if (indices.Count > 0) {
                            var descriptor = new AliasDescriptor();
                            foreach (string name in indices)
                                descriptor.Add(add => add.Index(name).Alias(index.Name));

                            response = await client.AliasAsync(descriptor).AnyContext();
                        }
                    } else {
                        response = await client.AliasAsync(a => a.Add(add => add.Index(index.VersionedName).Alias(index.Name))).AnyContext();
                    }

                    Debug.Assert(response != null && response.IsValid, response?.ServerError != null ? response.ServerError.Error : "An error occurred creating the alias.");
                }
                
                // already on current version
                if (currentVersion >= index.Version || currentVersion < 1)
                    continue;

                // upgrade
                await _workItemQueue.EnqueueAsync(new ReindexWorkItem {
                    OldIndex = String.Concat(index.Name, "-v", currentVersion),
                    NewIndex = index.VersionedName,
                    Alias = index.Name,
                    DeleteOld = true
                }).AnyContext();
            }
        }

        public async Task DeleteIndexesAsync(IElasticClient client) {
            var deleteResponse = await client.DeleteIndexAsync(i => i.AllIndices()).AnyContext();
            Debug.Assert(deleteResponse.IsValid, deleteResponse.ServerError != null ? deleteResponse.ServerError.Error : "An error occurred deleting the indexes.");

            foreach (var index in GetIndexes()) {
                var templatedIndex = index as ITemplatedElasticSeachIndex;
                if (templatedIndex != null) {
                    if ((await client.TemplateExistsAsync(index.VersionedName).AnyContext()).Exists) {
                        var response = await client.DeleteTemplateAsync(index.VersionedName).AnyContext();
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

        private async Task<int> GetAliasVersionAsync(IElasticClient client, string alias) {
            var res = await client.GetAliasAsync(a => a.Alias(alias)).AnyContext();
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
