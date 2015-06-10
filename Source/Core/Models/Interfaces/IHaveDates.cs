using System;

namespace Exceptionless.Core.Models {
    public interface IHaveDates : IHaveCreatedDate {
        DateTime ModifiedUtc { get; set; }
    }

    public interface IHaveCreatedDate {
        DateTime CreatedUtc { get; set; }
    }
}
