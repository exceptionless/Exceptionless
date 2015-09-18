using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepository<T> : IReadOnlyRepository<T> where T : class, IIdentity, new() {
        Task<T> AddAsync(T document, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true);
        Task AddAsync(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true);
        Task<T> SaveAsync(T document, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true);
        Task SaveAsync(ICollection<T> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotification = true);
        Task RemoveAsync(string id, bool sendNotification = true);
        Task RemoveAsync(T document, bool sendNotification = true);
        Task RemoveAsync(ICollection<T> documents, bool sendNotification = true);
        Task RemoveAllAsync();
        event EventHandler<DocumentChangeEventArgs<T>> DocumentChanging;
        event EventHandler<DocumentChangeEventArgs<T>> DocumentChanged;
    }
}