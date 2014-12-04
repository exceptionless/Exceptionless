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
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Models;
using FluentValidation;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public abstract class MongoRepository<T> : MongoReadOnlyRepository<T>, IRepository<T> where T : class, IIdentity, new() {
        protected readonly IValidator<T> _validator;
        protected readonly IMessagePublisher _messagePublisher;
        protected readonly static string _entityType = typeof(T).Name;
        protected readonly static bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByProject = typeof(IOwnedByProject).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByStack = typeof(IOwnedByStack).IsAssignableFrom(typeof(T));
        protected static readonly bool _isOrganization = typeof(T) == typeof(Organization);

        protected MongoRepository(MongoDatabase database, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(database, cacheClient) {
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

            _collection.InsertBatch<T>(documents);
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
            _collection.Remove(Query.In(CommonFieldNames.Id, documents.Select(d => _getIdValue(d.Id))));
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
            RemoveAll(new QueryOptions());
        }

        protected long RemoveAll(QueryOptions options, bool sendNotifications = true) {
            if (options == null)
                throw new ArgumentNullException("options");

            var fields = new List<string>(new[] { CommonFieldNames.Id });
            if (_isOwnedByOrganization)
                fields.Add(CommonFieldNames.OrganizationId);
            if (_isOwnedByProject)
                fields.Add(CommonFieldNames.ProjectId);
            if (_isOwnedByStack)
                fields.Add(CommonFieldNames.StackId);

            long recordsAffected = 0;

            var documents = Collection.FindAs<T>(options.GetMongoQuery(_getIdValue))
                .SetLimit(RepositoryConstants.BATCH_SIZE)
                .SetFields(fields.ToArray())
                .ToList();

            while (documents.Count > 0) {
                recordsAffected += documents.Count;
                Remove(documents, sendNotifications);

                documents = Collection.FindAs<T>(options.GetMongoQuery(_getIdValue))
                .SetLimit(RepositoryConstants.BATCH_SIZE)
                .SetFields(fields.ToArray())
                .ToList();
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

            foreach (var document in documents)
                _collection.Save(document);

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

        protected long UpdateAll(QueryOptions options, IMongoUpdate update, bool sendNotifications = true) {
            var result = _collection.Update(options.GetMongoQuery(_getIdValue), update, UpdateFlags.Multi);
            if (!sendNotifications || !EnableNotifications || _messagePublisher == null)
                return result.DocumentsAffected;

            if (options.OrganizationIds.Any()) {
                foreach (var orgId in options.OrganizationIds) {
                    PublishMessage(new EntityChanged {
                        ChangeType = EntityChangeType.UpdatedAll,
                        OrganizationId = orgId,
                        Type = _entityType
                    });
                }
            } else {
                PublishMessage(new EntityChanged {
                    ChangeType = EntityChangeType.UpdatedAll,
                    Type = _entityType
                });
            }

            return result.DocumentsAffected;
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