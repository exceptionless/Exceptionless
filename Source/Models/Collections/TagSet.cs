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

namespace Exceptionless.Models {
    public class TagSet : HashSet<string> {
        public TagSet() : base(StringComparer.OrdinalIgnoreCase) {}

        public TagSet(IEnumerable<string> values) : base(StringComparer.OrdinalIgnoreCase) {
            foreach (string value in values)
                Add(value);
        }

        public new IDisposable Add(string item) {
            base.Add(item);
            return new DisposableTag(this, item);
        }

        private class DisposableTag : IDisposable {
            private readonly TagSet _items;

            public DisposableTag(TagSet items, string value) {
                _items = items;
                Value = value;
            }

            public string Value { get; private set; }

            public void Dispose() {
                if (_items.Contains(Value))
                    _items.Remove(Value);
            }
        }
    }
}