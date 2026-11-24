namespace Exceptionless.Web.Api.Results;

/// <summary>Marks a successful handler result that is returned with the 202 Accepted status and a body.</summary>
public interface IAcceptedResponse
{
    object? Body { get; }
}

public record AcceptedResponse<T>(T Value) : IAcceptedResponse
{
    object? IAcceptedResponse.Body => Value;
}
