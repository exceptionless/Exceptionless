using System.Collections.Generic;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories
{
    public class DocumentChangeEventArgs<T> where T : class, IIdentity, new()
    {
        public DocumentChangeEventArgs(ChangeType changeType, ICollection<T> documents, IRepository<T> repository, ICollection<T> originalDocuments = null)
        {
            ChangeType = changeType;
            Documents = documents;
            Repository = repository;
            OriginalDocuments = originalDocuments;
        }

        public ChangeType ChangeType { get; private set; }
        public ICollection<T> Documents { get; private set; }
        public ICollection<T> OriginalDocuments { get; private set; }
        public IRepository<T> Repository { get; private set; }
    }
}