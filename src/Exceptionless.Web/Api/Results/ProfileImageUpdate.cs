namespace Exceptionless.Web.Api.Results;

public sealed record ProfileImageUpdate<T>(T View, string? PreviousFileName);
