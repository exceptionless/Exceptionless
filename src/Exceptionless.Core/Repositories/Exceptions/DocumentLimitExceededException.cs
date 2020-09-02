using System;

namespace Exceptionless.Core.Repositories.Base
{
    public class DocumentLimitExceededException : ApplicationException {
        public DocumentLimitExceededException(string message = null) : base(message) {
        }
    }
}