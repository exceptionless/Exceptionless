#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

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