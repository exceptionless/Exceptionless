using System;

namespace Exceptionless.Core.Repositories.Base {
    public class DocumentNotFoundException : ApplicationException {
        public DocumentNotFoundException() {}

        public DocumentNotFoundException(string id) {
            Id = id;
        }

        public string Id { get; private set; }

        public override string ToString() {
            if (!String.IsNullOrEmpty(Id))
                return String.Format("Document \"{0}\" could not be found", Id);

            return base.ToString();
        }
    }
}