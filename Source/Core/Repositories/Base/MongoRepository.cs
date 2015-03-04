using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public abstract class MongoRepository<T> : MongoReadOnlyRepository<T>, IRepository<T> where T : class, IIdentity, new() {
        protected readonly IValidator<T> _validator;
        protected readonly IMessagePublisher _messagePublisher;
        protected readonly static bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByProject = typeof(IOwnedByProject).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByStack = typeof(IOwnedByStack).IsAssignableFrom(typeof(T));
        protected static readonly bool _isUser = typeof(T) == typeof(User);

        protected MongoRepository(MongoDatabase database, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(database, cacheClient) {
            _validator = validator;
            _messagePublisher = messagePublisher;
        }

        public bool BatchNotifications { get; set; }

        public T Add(T document, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotifications = true) {
            if (document == null)
                throw new ArgumentNullException("document");

            Add(new[] { document }, addToCache, expiresIn, sendNotifications);
            return document;
        }

        protected virtual void BeforeAdd(ICollection<T> documents) { }

        public void Add(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotifications = true) {
            if (documents == null || documents.Count == 0)
                throw new ArgumentException("Must provide one or more documents to add.", "documents");

            BeforeAdd(documents);

            if (_validator != null)
                documents.ForEach(_validator.ValidateAndThrow);

            _collection.InsertBatch<T>(documents);
            AfterAdd(documents, addToCache, expiresIn, sendNotifications);
        }

        protected virtual void AfterAdd(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotifications = true) {
            if (!EnableCache && !sendNotifications)
                return;

            foreach (var document in documents) {
                if (EnableCache) {
                    InvalidateCache(document);
                    if (addToCache && Cache != null)
                        Cache.Set(GetScopedCacheKey(document.Id), document, expiresIn.HasValue ? expiresIn.Value : TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));
                }

                if (sendNotifications && !BatchNotifications)
                    PublishMessage(ChangeType.Added, document);
            }

            if (sendNotifications && BatchNotifications)
                PublishMessage(ChangeType.Added, documents);
        }

        public void Remove(string id, bool sendNotifications = true) {
            if (String.IsNullOrEmpty(id))
                throw new ArgumentNullException("id");

            var document = GetById(id, true);
            Remove(new[] { document }, sendNotifications);
        }

        public void Remove(T document, bool sendNotifications = true) {
            if (document == null)
                throw new ArgumentNullException("document");

            Remove(new[] { document }, sendNotifications);
        }

        protected virtual void BeforeRemove(ICollection<T> documents, bool sendNotifications = true) {
            foreach (var document in documents)
                InvalidateCache(document);
        }

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

                if (sendNotification && !BatchNotifications)
                    PublishMessage(ChangeType.Removed, document);
            }

            if (sendNotification && BatchNotifications)
                PublishMessage(ChangeType.Removed, documents);
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
            if (_isUser)
                fields.AddRange(new [] { "OrganizationIds", "EmailAddress" });

            long recordsAffected = 0;

            var documents = Collection.FindAs<T>(options.GetMongoQuery(_getIdValue))
                .SetLimit(Settings.Current.BulkBatchSize)
                .SetFields(fields.ToArray())
                .ToList();

            while (documents.Count > 0) {
                recordsAffected += documents.Count;
                Remove(documents, sendNotifications);

                documents = Collection.FindAs<T>(options.GetMongoQuery(_getIdValue))
                .SetLimit(Settings.Current.BulkBatchSize)
                .SetFields(fields.ToArray())
                .ToList();
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

            foreach (var document in documents)
                _collection.Save(document);

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

        protected long UpdateAll(QueryOptions options, IMongoUpdate update, bool sendNotifications = true) {
            var result = _collection.Update(options.GetMongoQuery(_getIdValue), update, UpdateFlags.Multi);
            if (!sendNotifications || _messagePublisher == null)
                return result.DocumentsAffected;

            if (options.OrganizationIds.Any()) {
                foreach (var orgId in options.OrganizationIds) {
                    PublishMessage(new EntityChanged {
                        ChangeType = ChangeType.Saved,
                        OrganizationId = orgId,
                        Type = _entityType
                    });
                }
            } else {
                PublishMessage(new EntityChanged {
                    ChangeType = ChangeType.Saved,
                    Type = _entityType
                });
            }

            return result.DocumentsAffected;
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

                    PublishMessage(message);
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

                    PublishMessage(message);
                }
            } else {
                foreach (var doc in documents) {
                    var message = new EntityChanged {
                        ChangeType = changeType,
                        Id = doc.Id,
                        Type = _entityType
                    };

                    PublishMessage(message);
                }
            }
        }

        protected void PublishMessage<TMessageType>(TMessageType message) where TMessageType : class {
            if (_messagePublisher != null)
                _messagePublisher.Publish(message);
        }
    }
}