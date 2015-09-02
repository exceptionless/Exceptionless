using System;

namespace Exceptionless.Core.Models {
    public interface IOwnedByProject {
        /// <summary>
        /// The project that the document belongs to.
        /// </summary>
        string ProjectId { get; set; }
    }
}