namespace Exceptionless.Core.Models {
    public interface ISupportSoftDeletes {
        bool IsDeleted { get; set; }
    }
}