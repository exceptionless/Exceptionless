using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Exceptionless.Core.Models.Ingestion;

/// <summary>
/// A compact V3 ingestion event. Organization and project ownership come from
/// the authenticated request rather than the event payload.
/// </summary>
public sealed record EventIngestionV3Event
{
    [Required]
    [StringLength(EventIngestionV3Limits.MaximumEventIdLength, MinimumLength = 1)]
    public required string Id { get; init; }

    [Required]
    [StringLength(EventIngestionV3Limits.MaximumTypeLength, MinimumLength = 1)]
    public required string Type { get; init; }

    public DateTimeOffset? Date { get; init; }

    [StringLength(EventIngestionV3Limits.MaximumSourceLength, MinimumLength = 1)]
    public string? Source { get; init; }

    [StringLength(EventIngestionV3Limits.MaximumMessageLength, MinimumLength = 1)]
    public string? Message { get; init; }

    [StringLength(EventIngestionV3Limits.MaximumReferenceIdLength, MinimumLength = 1)]
    public string? ReferenceId { get; init; }

    public decimal? Value { get; init; }

    public string[]? Tags { get; init; }

    [StringLength(EventIngestionV3Limits.MaximumVersionLength, MinimumLength = 1)]
    public string? Version { get; init; }

    [StringLength(EventIngestionV3Limits.MaximumLevelLength, MinimumLength = 1)]
    public string? Level { get; init; }

    public EventIngestionV3Client? Client { get; init; }

    [StringLength(EventIngestionV3Limits.MaximumExceptionTypeLength, MinimumLength = 1)]
    public string? ExceptionType { get; init; }

    [StringLength(EventIngestionV3Limits.MaximumStackTraceLength, MinimumLength = 1)]
    public string? StackTrace { get; init; }

    public EventIngestionV3Stacking? Stacking { get; init; }

    public EventIngestionV3User? User { get; init; }

    public EventIngestionV3Request? Request { get; init; }

    public EventIngestionV3Environment? Environment { get; init; }

    public JsonElement? Data { get; init; }
}

public sealed record EventIngestionV3Client
{
    [Required]
    [StringLength(EventIngestionV3Limits.MaximumClientNameLength, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required]
    [StringLength(EventIngestionV3Limits.MaximumClientVersionLength, MinimumLength = 1)]
    public required string Version { get; init; }
}

public sealed record EventIngestionV3Stacking
{
    [StringLength(EventIngestionV3Limits.MaximumMessageLength, MinimumLength = 1)]
    public string? Title { get; init; }

    public required Dictionary<string, string> SignatureData { get; init; }
}

public sealed record EventIngestionV3User
{
    [StringLength(EventIngestionV3Limits.MaximumUserIdentityLength, MinimumLength = 1)]
    public string? Identity { get; init; }

    [StringLength(EventIngestionV3Limits.MaximumUserNameLength, MinimumLength = 1)]
    public string? Name { get; init; }

    public JsonElement? Data { get; init; }
}

public sealed record EventIngestionV3Request
{
    public string? UserAgent { get; init; }
    public string? HttpMethod { get; init; }
    public bool? IsSecure { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? Path { get; init; }
    public string? Referrer { get; init; }
    public string? ClientIpAddress { get; init; }
    public Dictionary<string, string[]>? Headers { get; init; }
    public Dictionary<string, string>? Cookies { get; init; }
    public Dictionary<string, string>? QueryString { get; init; }
    public JsonElement? PostData { get; init; }
    public JsonElement? Data { get; init; }
}

public sealed record EventIngestionV3Environment
{
    public string? Architecture { get; init; }
    public string? OSName { get; init; }
    public string? OSVersion { get; init; }
    public string? MachineName { get; init; }
    public string? RuntimeVersion { get; init; }
    public string? ProcessName { get; init; }
    public string? ProcessId { get; init; }
    public string? ThreadName { get; init; }
    public string? ThreadId { get; init; }
    public int? ProcessorCount { get; init; }
    public long? TotalPhysicalMemory { get; init; }
    public long? AvailablePhysicalMemory { get; init; }
    public long? ProcessMemorySize { get; init; }
    public JsonElement? Data { get; init; }
}
