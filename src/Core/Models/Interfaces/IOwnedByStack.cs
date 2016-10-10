using System;

namespace Exceptionless.Core.Models {
    public interface IOwnedByStack {
        /// <summary>
        /// The stack that the document belongs to.
        /// </summary>
        string StackId { get; set; }
    }
}