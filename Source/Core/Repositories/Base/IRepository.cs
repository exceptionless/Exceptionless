using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepository<T> : IReadOnlyRepository<T> where T : class, IIdentity, new() {
        T Add(T document, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true);
        void Add(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true);
        T Save(T document, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true);
        void Save(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true);
        void Remove(string id, bool sendNotification = true);
        void Remove(T document, bool sendNotification = true);
        void Remove(ICollection<T> documents, bool sendNotification = true);
        void RemoveAll();
    }
}