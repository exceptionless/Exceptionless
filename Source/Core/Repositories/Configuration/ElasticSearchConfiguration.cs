using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public IElasticClient GetClient(IEnumerable<Uri> serverUris) {
            var connectionPool = new StaticConnectionPool(serverUris);
            var indexes = GetIndexes();
            var settings = new ConnectionSettings(connectionPool)
                .SetDefaultIndex("_all")
                .MapDefaultTypeIndices(t => t.AddRange(indexes.SelectMany(idx => idx.GetTypeIndices())))
                .MapDefaultTypeNames(t => t.AddRange(indexes.SelectMany(idx => idx.GetIndexTypeNames())))
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

        public void ConfigureIndexes(IElasticClient client, bool deleteExisting = false) {
            if (deleteExisting) {
                var deleteResponse = client.DeleteIndex(i => i.AllIndices());
                Debug.Assert(deleteResponse.IsValid, deleteResponse.ServerError != null ? deleteResponse.ServerError.Error : "An error occurred deleting the indexes.");
            }

            var indexes = GetIndexes();
            foreach (var index in indexes) {
                IIndicesOperationResponse response = null;
                int currentVersion = GetAliasVersion(client, index.Name);

                var templatedIndex = index as ITemplatedElasticSeachIndex;
                if (templatedIndex != null) {
                    if (deleteExisting && client.TemplateExists(index.VersionedName).Exists) {
                        response = client.DeleteTemplate(index.VersionedName);
                        Debug.Assert(response.IsValid, response.ServerError != null ? response.ServerError.Error : "An error occurred deleting the index template.");
                    }

                    response = client.PutTemplate(index.VersionedName, template => templatedIndex.CreateTemplate(template));
                } else if (!client.IndexExists(index.VersionedName).Exists) {
                    response = client.CreateIndex(index.VersionedName, descriptor => index.CreateIndex(descriptor));
                }

                Debug.Assert(response != null && response.IsValid, response != null && response.ServerError != null ? response.ServerError.Error : "An error occurred creating the index or template.");

                if (templatedIndex == null && !client.AliasExists(index.Name).Exists) {
                    if (templatedIndex != null)
                        response = client.Alias(a => a.Add(add => add.Alias(index.Name)));
                    else
                        response = client.Alias(a => a.Add(add => add.Index(index.VersionedName).Alias(index.Name)));

                    Debug.Assert(response != null && response.IsValid, response != null && response.ServerError != null ? response.ServerError.Error : "An error occurred creating the alias.");
                }

                // already on current version
                if (currentVersion >= index.Version || currentVersion < 1)
                    continue;

                // upgrade
                _workItemQueue.Enqueue(new ReindexWorkItem {
                    OldIndex = String.Concat(index.Name, "-v", currentVersion),
                    NewIndex = index.VersionedName,
                    Alias = index.Name,
                    DeleteOld = true
                });
            }
        }

        private IEnumerable<IElasticSearchIndex> GetIndexes() {
            return new IElasticSearchIndex[] {
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
