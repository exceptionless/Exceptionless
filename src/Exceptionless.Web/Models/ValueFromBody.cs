using System.Diagnostics;

namespace Exceptionless.Web.Models {
    [DebuggerDisplay("{Value}")]
    public class ValueFromBody<T> {
        private ValueFromBody() {}
        
        public ValueFromBody(T value) {
            Value = value;
        }

        public T Value { get; set; }
    }
}
