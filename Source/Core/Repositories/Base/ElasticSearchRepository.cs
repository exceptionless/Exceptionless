#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Extensions;
using Elasticsearch.Net;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Models;
using FluentValidation;
using Nest;
using NLog.Fluent;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchRepository<T> : ElasticSearchReadOnlyRepository<T>, IRepository<T> where T : class, IIdentity, new() {
        protected readonly IValidator<T> _validator;
        protected readonly IMessagePublisher _messagePublisher;
        protected readonly static string _entityType = typeof(T).Name;
        protected readonly static bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByProject = typeof(IOwnedByProject).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByStack = typeof(IOwnedByStack).IsAssignableFrom(typeof(T));
        protected static readonly bool _isOrganization = typeof(T) == typeof(Organization);

        protected ElasticSearchRepository(IElasticClient elasticClient, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, cacheClient) {
            _validator = validator;
            _messagePublisher = messagePublisher;
            EnableNotifications = true;
        }

        public bool EnableNotifications { get; set; }

        public T Add(T document, bool addToCache = false, TimeSpan? expiresIn = null) {
            if (document == null)
                throw new ArgumentNullException("document");

            Add(new[] { document }, addToCache, expiresIn);
            return document;
        }

        protected virtual void BeforeAdd(ICollection<T> documents) { }

        public void Add(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            if (documents == null || documents.Count == 0)
                throw new ArgumentException("Must provide one or more documents to add.", "documents");

            BeforeAdd(documents);

            if (_validator != null)
                documents.ForEach(_validator.ValidateAndThrow);

            if (_isEvent)
                foreach (var group in documents.Cast<PersistentEvent>().GroupBy(e => e.Date.ToUniversalTime().Date)) {
                    var result = _elasticClient.IndexMany(group.ToList(), type: "events", index: String.Concat(EventsIndexName, "-", group.Key.ToString("yyyyMM")));
                    if (!result.IsValid)
                        throw new ArgumentException(String.Join("\r\n", result.ItemsWithErrors.Select(i => i.Error)));
                }
            else {
                var result = _elasticClient.IndexMany(documents);
                if (!result.IsValid)
                    throw new ArgumentException(String.Join("\r\n", result.ItemsWithErrors.Select(i => i.Error)));
            }

            AfterAdd(documents, addToCache, expiresIn);
        }

        protected virtual void AfterAdd(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            foreach (var document in documents) {
                InvalidateCache(document);
                if (addToCache && Cache != null)
                    Cache.Set(GetScopedCacheKey(document.Id), document, expiresIn.HasValue ? expiresIn.Value : TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));

                if (EnableNotifications)
                    PublishMessage(EntityChangeType.Added, document);
            }
        }

        public void Remove(string id) {
            if (String.IsNullOrEmpty(id))
                throw new ArgumentNullException("id");

            // TODO: Decide if it's worth it to retrieve the document first
            var document = GetById(id, true);
            Remove(new[] { document });
        }

        public void Remove(T document) {
            if (document == null)
                throw new ArgumentNullException("document");

            Remove(new[] { document });
        }

        protected virtual void BeforeRemove(ICollection<T> documents) { }

        public void Remove(ICollection<T> documents, bool sendNotification = true) {
            if (documents == null || documents.Count == 0)
                throw new ArgumentException("Must provide one or more documents to remove.", "documents");

            BeforeRemove(documents);
            _elasticClient.DeleteByQuery<T>(q => q.Query(q1 => q1.Ids(documents.Select(d => d.Id))));
            AfterRemove(documents, sendNotification);
        }

        protected virtual void AfterRemove(ICollection<T> documents, bool sendNotification = true) {
            foreach (var document in documents) {
                InvalidateCache(document);

                if (sendNotification && EnableNotifications)
                    PublishMessage(EntityChangeType.Removed, document);
            }
        }

        public void RemoveAll() {
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

            long recordsAffected = 0;

            var searchDescriptor = new SearchDescriptor<T>()
                .Filter(options.GetElasticSearchFilter<T>() ?? Filter<T>.MatchAll())
                .Source(s => s.Include(fields.ToArray()))
                .Size(RepositoryConstants.BATCH_SIZE);

            var documents = _elasticClient.Search<T>(searchDescriptor).Documents.ToList();
            while (documents.Count > 0) {
                recordsAffected += documents.Count;
                Remove(documents, sendNotifications);

                documents = _elasticClient.Search<T>(searchDescriptor).Documents.ToList();
            }

            return recordsAffected;
        }

        public T Save(T document, bool addToCache = false, TimeSpan? expiresIn = null) {
            if (document == null)
                throw new ArgumentNullException("document");

            Save(new[] { document }, addToCache, expiresIn);
            return document;
        }

        protected virtual void BeforeSave(ICollection<T> documents) { }

        public void Save(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            if (documents == null || documents.Count == 0)
                throw new ArgumentException("Must provide one or more documents to save.", "documents");

            BeforeSave(documents);

            if (_validator != null)
                documents.ForEach(_validator.ValidateAndThrow);

            if (_isEvent)
                foreach (var group in documents.Cast<PersistentEvent>().GroupBy(e => e.Date.ToUniversalTime().Date)) {
                    var result = _elasticClient.IndexMany(group.ToList(), type: "events", index: String.Concat(EventsIndexName, "-", group.Key.ToString("yyyyMM")));
                    if (!result.IsValid)
                        throw new ArgumentException(String.Join("\r\n", result.ItemsWithErrors.Select(i => i.Error)));
            } else {
                var result = _elasticClient.IndexMany(documents);
                if (!result.IsValid)
                    throw new ArgumentException(String.Join("\r\n", result.ItemsWithErrors.Select(i => i.Error)));
            }

            AfterSave(documents, addToCache, expiresIn);
        }

        protected virtual void AfterSave(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            foreach (var document in documents) {
                InvalidateCache(document);
                if (addToCache && Cache != null)
                    Cache.Set(GetScopedCacheKey(document.Id), document, expiresIn.HasValue ? expiresIn.Value : TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));

                if (EnableNotifications)
                    PublishMessage(EntityChangeType.Saved, document);
            }
        }

        protected long UpdateAll(string organizationId, QueryOptions options, object update, bool sendNotifications = true) {
            long recordsAffected = 0;

            var searchDescriptor = new SearchDescriptor<T>()
                .Filter(options.GetElasticSearchFilter<T>() ?? Filter<T>.MatchAll())
                .Source(s => s.Include(f => f.Id))
                .SearchType(SearchType.Scan)
                .Scroll("4s")
                .Size(RepositoryConstants.BATCH_SIZE);

            var scanResults = _elasticClient.Search<T>(searchDescriptor);
            var results = _elasticClient.Scroll<T>("4s", scanResults.ScrollId);
            while (results.Hits.Any()) {
                var bulkResult = _elasticClient.Bulk(b => {
                    var script = update as string;
                    if (script != null)
                        results.Hits.ForEach(h => b.Update<T>(u => u.Id(h.Id).Index(h.Index).Script(script)));
                    else
                        results.Hits.ForEach(h => b.Update<T, object>(u => u.Id(h.Id).Index(h.Index).Doc(update)));

                    return b;
                });

                if (!bulkResult.IsValid) {
                    Log.Error().Message("Error occurred while bulk updating");
                    return 0;
                }

                results.Hits.ForEach(d => InvalidateCache(d.Id));

                recordsAffected += results.Documents.Count();
                results = _elasticClient.Scroll<T>("4s", results.ScrollId);
            }

            if (EnableNotifications && sendNotifications) {
                PublishMessage(new EntityChanged {
                    ChangeType = EntityChangeType.UpdatedAll,
                    OrganizationId = organizationId,
                    Type = _entityType
                });
            }

            return recordsAffected;
        }

        protected virtual void PublishMessage(EntityChangeType changeType, T document) {
            var orgEntity = document as IOwnedByOrganization;
            var message = new EntityChanged {
                ChangeType = changeType,
                Id = document.Id,
                OrganizationId = orgEntity != null ? orgEntity.OrganizationId : null,
                Type = _entityType
            };

            PublishMessage(message);
        }

        protected void PublishMessage<TMessageType>(TMessageType message) where TMessageType : class {
            if (_messagePublisher != null)
                _messagePublisher.Publish(message);
        }
    }
}