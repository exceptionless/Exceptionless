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
using CodeSmith.Core.Component;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Migrations.Documents {
    public class DocumentUpgrader : SingletonBase<DocumentUpgrader> {
        private static readonly Dictionary<Type, SortedDictionary<int, Action<JObject>>> _documentUpgrades = new Dictionary<Type, SortedDictionary<int, Action<JObject>>>();

        public bool CanUpgradeType(Type type) {
            return _documentUpgrades.ContainsKey(type);
        }

        public JObject Upgrade<T>(JObject document) {
            return Upgrade(document, typeof(T));
        }

        public JObject Upgrade(JObject document, Type type) {
            if (!_documentUpgrades.ContainsKey(type))
                return document;

            foreach (var migration in _documentUpgrades[type]) {
                try {
                    migration.Value(document);
                } catch (Exception ex) {
                    ex.ToExceptionless()
                        .AddObject(new object[] { migration.Key, document.ToString() }, "Migration")
                        .Submit();
                }
            }

            return document;
        }

        public void Add<T>(int version, Action<JObject> action) {
            if (!_documentUpgrades.ContainsKey(typeof(T))) {
                _documentUpgrades.Add(typeof(T), new SortedDictionary<int, Action<JObject>> {
                    { version, action }
                });
            } else
                _documentUpgrades[typeof(T)][version] = action;
        }
    }
}