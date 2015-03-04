using System;

namespace Exceptionless.Core.Models {
    public interface IIdentity {
        /// <summary>
        /// Unique id that identifies a document.
        /// </summary>
        string Id { get; }
    }
}