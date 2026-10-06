using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Models.Ingestion;

/// <summary>
/// One event submitted to the V3 ingestion API. Every property is optional; the smallest useful
/// event is <c>{"message":"..."}</c>. Organization and project ownership come from the
/// authenticated request. Nested objects use the same shapes as the V2 event data values, so V2
/// and V3 events are processed, grouped, and stored identically.
/// </summary>
public sealed record EventIngestionV3Event
{
    /// <summary>
    /// A client-generated identifier such as a UUID. Resending an event with the same id within the
    /// idempotency window acknowledges it as a duplicate instead of storing it twice.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// The event type, such as <c>error</c>, <c>log</c>, <c>usage</c>, <c>404</c>, or a custom type.
    /// Defaults to <c>error</c> when error information is present and <c>log</c> otherwise.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>When the event occurred. Defaults to the time the server received it.</summary>
    public DateTimeOffset? Date { get; init; }

    /// <summary>The event source, such as a logger name or request path.</summary>
    public string? Source { get; init; }

    /// <summary>The event message.</summary>
    public string? Message { get; init; }

    /// <summary>An application-defined identifier that can be used to look up the event.</summary>
    public string? ReferenceId { get; init; }

    /// <summary>A numeric value associated with the event.</summary>
    public decimal? Value { get; init; }

    /// <summary>Tags that categorize the event.</summary>
    public string[]? Tags { get; init; }

    /// <summary>The application version that produced the event.</summary>
    public string? Version { get; init; }

    /// <summary>The log level, such as <c>info</c> or <c>error</c>.</summary>
    public string? Level { get; init; }

    /// <summary>The runtime's exception type name. Used with <c>stack_trace</c>.</summary>
    public string? ExceptionType { get; init; }

    /// <summary>The runtime's original stack trace text. Used with <c>exception_type</c>.</summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// A structured error with parsed stack frames, in the same format as the V2 <c>@error</c> data
    /// value. Takes precedence over <c>exception_type</c> and <c>stack_trace</c>.
    /// </summary>
    public Error? Error { get; init; }

    /// <summary>Overrides automatic grouping with a stack title and signature values.</summary>
    public ManualStackingInfo? Stacking { get; init; }

    /// <summary>The user that the event happened to.</summary>
    public UserInfo? User { get; init; }

    /// <summary>The HTTP request that the event happened during.</summary>
    public RequestInfo? Request { get; init; }

    /// <summary>The machine and process that produced the event.</summary>
    public EnvironmentInfo? Environment { get; init; }

    /// <summary>Additional custom data. Values for the first-class properties above take precedence.</summary>
    public DataDictionary? Data { get; init; }
}
