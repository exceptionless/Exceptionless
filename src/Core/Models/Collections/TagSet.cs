using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models {
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