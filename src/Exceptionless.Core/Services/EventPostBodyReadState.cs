namespace Exceptionless.Core.Services;

public interface IEventPostBodyReadState
{
    int? RejectedStatusCode { get; }
    string? RejectionReason { get; }
}
