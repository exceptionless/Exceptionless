using System.Text.Json.Serialization;
using Exceptionless.Core.Models.Ingestion;

namespace Exceptionless.Core.Serialization;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    MaxDepth = EventIngestionV3Limits.MaximumJsonDepth,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = false)]
[JsonSerializable(typeof(EventIngestionV3Event))]
[JsonSerializable(typeof(EventIngestionV3Event[]))]
[JsonSerializable(typeof(EventIngestionV3User))]
[JsonSerializable(typeof(EventIngestionV3Request))]
[JsonSerializable(typeof(EventIngestionV3Environment))]
public sealed partial class EventIngestionJsonContext : JsonSerializerContext;
