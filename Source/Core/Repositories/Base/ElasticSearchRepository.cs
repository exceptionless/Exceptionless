using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;
using NLog.Fluent;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchRepository<T> : ElasticSearchReadOnlyRepository<T>, IRepository<T> where T : class, IIdentity, new() {
        protected readonly IValidator<T> _validator;
        protected readonly IMessagePublisher _messagePublisher;
        protected readonly static bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByProject = typeof(IOwnedByProject).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByStack = typeof(IOwnedByStack).IsAssignableFrom(typeof(T));

        protected ElasticSearchRepository(IElasticClient elasticClient, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, cacheClient) {
            _validator = validator;
            _messagePublisher = messagePublisher;
        }

        public bool BatchNotifications { get; set; }

        public T Add(T document, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true) {
            if (document == null)
                throw new ArgumentNullException("document");

            Add(new[] { document }, addToCache, expiresIn, sendNotification);
            return document;
        }

        protected virtual void BeforeAdd(ICollection<T> documents) { }

        public void Add(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true) {
            if (documents == null || documents.Count == 0)
                throw new ArgumentException("Must provide one or more documents to add.", "documents");

            BeforeAdd(documents);

            if (_validator != null)
                documents.ForEach(_validator.ValidateAndThrow);

            if (_isEvent)
                foreach (var group in documents.Cast<PersistentEvent>().GroupBy(e => e.Date.ToUniversalTime().Date)) {
                    var result = _elasticClient.IndexMany(group.ToList(), type: "events", index: String.Concat(EventsIndexName, "-", group.Key.ToString("yyyyMM")));
                    if (!result.IsValid)
                        throw new ApplicationException(String.Join("\r\n", result.ItemsWithErrors.Select(i => i.Error)), result.ConnectionStatus.OriginalException);
                }
            else {
                var result = _elasticClient.IndexMany(documents);
                if (!result.IsValid)
                    throw new ApplicationException(String.Join("\r\n", result.ItemsWithErrors.Select(i => i.Error)), result.ConnectionStatus.OriginalException);
            }

            AfterAdd(documents, addToCache, expiresIn, sendNotification);
        }

        protected virtual void AfterAdd(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true) {
            if (!EnableCache && !sendNotification)
                return;
            
            foreach (var document in documents) {
                if (EnableCache) {
                    InvalidateCache(document);
                    if (addToCache && Cache != null)
                        Cache.Set(GetScopedCacheKey(document.Id), document, expiresIn.HasValue ? expiresIn.Value : TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));
                }

                if (sendNotification && !BatchNotifications)
                    PublishMessage(ChangeType.Added, document);
            }

            if (sendNotification && BatchNotifications)
                PublishMessage(ChangeType.Added, documents);
        }

        public void Remove(string id, bool sendNotification = true) {
            if (String.IsNullOrEmpty(id))
                throw new ArgumentNullException("id");

            var document = GetById(id, true);
            Remove(new[] { document }, sendNotification);
        }

        public void Remove(T document, bool sendNotification = true) {
            if (document == null)
                throw new ArgumentNullException("document");

            Remove(new[] { document }, sendNotification);
        }

        protected virtual void BeforeRemove(ICollection<T> documents) {
            if (EnableCache)
                documents.ForEach(InvalidateCache);
        }

        public void Remove(ICollection<T> documents, bool sendNotification = true) {
            if (documents == null || documents.Count == 0)
                throw new ArgumentException("Must provide one or more documents to remove.", "documents");

            BeforeRemove(documents);
            _elasticClient.DeleteByQuery<T>(q => q.Query(q1 => q1.Ids(documents.Select(d => d.Id))));
            AfterRemove(documents, sendNotification);
        }

        protected virtual void AfterRemove(ICollection<T> documents, bool sendNotification = true) {
            if (!EnableCache && !sendNotification)
                return;

            foreach (var document in documents) {
                if (EnableCache)
                    InvalidateCache(document);

                if (sendNotification && !BatchNotifications)
                    PublishMessage(ChangeType.Removed, document);
            }

            if (sendNotification && BatchNotifications)
                PublishMessage(ChangeType.Removed, documents);
        }

        public void RemoveAll() {
            if (EnableCache)
                Cache.FlushAll();

            if (_isEvent)
                _elasticClient.DeleteIndex(d => d.Index(String.Concat(EventsIndexName, "-*")));
            else if (_isStack)
                _elasticClient.DeleteIndex(d => d.Index(StacksIndexName));
            else
                RemoveAll(new QueryOptions(), false);
        }

        protected long RemoveAll(QueryOptions options, bool sendNotifications = true) {
            if (options == null)
                throw new ArgumentNullException("options");

            var fields = new List<string>(new[] { "id" });//CommonFieldNames.Id });
            if (_isOwnedByOrganization)
                fields.Add("organization_id");//CommonFieldNames.OrganizationId);
            if (_isOwnedByProject)
                fields.Add("project_id");//CommonFieldNames.ProjectId);
            if (_isOwnedByStack)
                fields.Add("stack_id");//CommonFieldNames.StackId);
            if (_isStack)
                fields.Add("signature_hash");

            long recordsAffected = 0;

            var searchDescriptor = new SearchDescriptor<T>()
                .Filter(options.GetElasticSearchFilter<T>() ?? Filter<T>.MatchAll())
                .Source(s => s.Include(fields.ToArray()))
                .Size(Settings.Current.BulkBatchSize);

            var documents = _elasticClient.Search<T>(searchDescriptor).Documents.ToList();
            while (documents.Count > 0) {
                recordsAffected += documents.Count;
                Remove(documents, sendNotifications);

                documents = _elasticClient.Search<T>(searchDescriptor).Documents.ToList();
            }

            return recordsAffected;
        }

        public T Save(T document, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotifications = true) {
            if (document == null)
                throw new ArgumentNullException("document");

            Save(new[] { document }, addToCache, expiresIn, sendNotifications);
            return document;
        }

        protected virtual void BeforeSave(ICollection<T> originalDocuments, ICollection<T> documents) { }

        public void Save(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotifications = true) {
            if (documents == null || documents.Count == 0)
                throw new ArgumentException("Must provide one or more documents to save.", "documents");

            string[] ids = documents.Where(d => !String.IsNullOrEmpty(d.Id)).Select(d => d.Id).ToArray();
            var originalDocuments = ids.Length > 0 ? GetByIds(documents.Select(d => d.Id).ToArray()) : new List<T>();

            BeforeSave(originalDocuments, documents);

            if (_validator != null)
                documents.ForEach(_validator.ValidateAndThrow);

            if (_isEvent)
                foreach (var group in documents.Cast<PersistentEvent>().GroupBy(e => e.Date.ToUniversalTime().Date)) {
                    var result = _elasticClient.IndexMany(group.ToList(), type: "events", index: String.Concat(EventsIndexName, "-", group.Key.ToString("yyyyMM")));
                    if (!result.IsValid)
                        throw new ApplicationException(String.Join("\r\n", result.ItemsWithErrors.Select(i => i.Error)), result.ConnectionStatus.OriginalException);
            } else {
                var result = _elasticClient.IndexMany(documents);
                if (!result.IsValid)
                    throw new ApplicationException(String.Join("\r\n", result.ItemsWithErrors.Select(i => i.Error)), result.ConnectionStatus.OriginalException);
            }

            AfterSave(originalDocuments, documents, addToCache, expiresIn, sendNotifications);
        }

        protected virtual void AfterSave(ICollection<T> originalDocuments, ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotifications = true) {
            if (!EnableCache && !sendNotifications)
                return;

            if (EnableCache) 
                originalDocuments.ForEach(InvalidateCache);

            foreach (var document in documents) {
                if (EnableCache && addToCache && Cache != null)
                    Cache.Set(GetScopedCacheKey(document.Id), document, expiresIn.HasValue ? expiresIn.Value : TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));

                if (sendNotifications && !BatchNotifications)
                    PublishMessage(ChangeType.Saved, document);
            }

            if (sendNotifications && BatchNotifications)
                PublishMessage(ChangeType.Saved, documents);
        }

        protected long UpdateAll(string organizationId, QueryOptions options, object update, bool sendNotifications = true) {
            return UpdateAll(new[] { organizationId }, options, update, sendNotifications);
        }

        protected long UpdateAll(string[] organizationIds, QueryOptions options, object update, bool sendNotifications = true) {
            long recordsAffected = 0;

            var searchDescriptor = new SearchDescriptor<T>()
                .Filter(options.GetElasticSearchFilter<T>() ?? Filter<T>.MatchAll())
                .Source(s => s.Include(f => f.Id))
                .SearchType(SearchType.Scan)
                .Scroll("4s")
                .Size(Settings.Current.BulkBatchSize);

            _elasticClient.EnableTrace();
            var scanResults = _elasticClient.Search<T>(searchDescriptor);
            _elasticClient.DisableTrace();

            // Check to see if no scroll id was returned. This will occur when the index doesn't exist.
            if (scanResults.IsValid && String.IsNullOrEmpty(scanResults.ScrollId))
                return 0;

            var results = _elasticClient.Scroll<T>("4s", scanResults.ScrollId);
            while (results.Hits.Any()) {
                var bulkResult = _elasticClient.Bulk(b => {
                    string script = update as string;
                    if (script != null)
                        results.Hits.ForEach(h => b.Update<T>(u => u.Id(h.Id).Index(h.Index).Script(script)));
                    else
                        results.Hits.ForEach(h => b.Update<T, object>(u => u.Id(h.Id).Index(h.Index).Doc(update)));

                    return b;
                });

                if (!bulkResult.IsValid) {
                    Log.Error().Message("Error occurred while bulk updating").Exception(bulkResult.ConnectionStatus.OriginalException).Write();
                    return 0;
                }

                if (EnableCache)
                    results.Hits.ForEach(d => InvalidateCache(d.Id));

                recordsAffected += results.Documents.Count();
                results = _elasticClient.Scroll<T>("4s", results.ScrollId);
            }

            if (recordsAffected <= 0)
                return 0;

            if (!sendNotifications)
                return recordsAffected;

            foreach (var organizationId in organizationIds) {
                PublishMessage(new EntityChanged {
                    ChangeType = ChangeType.Saved,
                    OrganizationId = organizationId,
                    Type = _entityType
                }, TimeSpan.FromSeconds(1.5));
            }

            return recordsAffected;
        }

        protected void PublishMessage(ChangeType changeType, T document) {
            PublishMessage(changeType, new[] { document });
        }

        protected virtual void PublishMessage(ChangeType changeType, IEnumerable<T> documents) {
            if (_isOwnedByOrganization && _isOwnedByProject) {
                foreach (var projectDocs in documents.Cast<IOwnedByOrganizationAndProjectWithIdentity>().GroupBy(d => d.ProjectId)) {
                    var firstDoc = projectDocs.FirstOrDefault();
                    if (firstDoc == null)
                        continue;

                    int count = projectDocs.Count();
                    var message = new EntityChanged {
                        ChangeType = changeType,
                        OrganizationId = firstDoc.OrganizationId,
                        ProjectId = projectDocs.Key,
                        Id = count == 1 ? firstDoc.Id : null,
                        Type = _entityType
                    };

                    PublishMessage(message, TimeSpan.FromSeconds(1.5));
                }
            } else if (_isOwnedByOrganization) {
                foreach (var orgDocs in documents.Cast<IOwnedByOrganizationWithIdentity>().GroupBy(d => d.OrganizationId)) {
                    var firstDoc = orgDocs.FirstOrDefault();
                    if (firstDoc == null)
                        continue;

                    int count = orgDocs.Count();
                    var message = new EntityChanged {
                        ChangeType = changeType,
                        OrganizationId = orgDocs.Key,
                        Id = count == 1 ? firstDoc.Id : null,
                        Type = _entityType
                    };

                    PublishMessage(message, TimeSpan.FromSeconds(1.5));
                }
            } else {
                foreach (var doc in documents) {
                    var message = new EntityChanged {
                        ChangeType = changeType,
                        Id = doc.Id,
                        Type = _entityType
                    };

                    PublishMessage(message, TimeSpan.FromSeconds(1.5));
                }
            }
        }

        protected void PublishMessage<TMessageType>(TMessageType message, TimeSpan? delay = null) where TMessageType : class {
            if (_messagePublisher != null)
                _messagePublisher.Publish(message, delay);
        }
    }
}