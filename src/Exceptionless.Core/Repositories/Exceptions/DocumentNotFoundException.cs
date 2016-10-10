using System;

namespace Exceptionless.Core.Repositories.Base {
    public class DocumentNotFoundException : ApplicationException {
        public DocumentNotFoundException(string id, string message = null) : base(message) {
            Id = id;
        }

        public string Id { get; private set; }

        public override string ToString() {
            if (!String.IsNullOrEmpty(Message))
                return Message;

            if (!String.IsNullOrEmpty(Id))
                return $"Document \"{Id}\" could not be found";

            return base.ToString();
        }
    }
}
