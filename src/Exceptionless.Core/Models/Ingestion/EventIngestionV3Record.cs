namespace Exceptionless.Core.Models.Ingestion;

/// <summary>A V3 event and its zero-based position in the request.</summary>
public readonly record struct EventIngestionV3Record(int Index, EventIngestionV3Event Event);
