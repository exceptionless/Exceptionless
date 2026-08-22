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
[JsonSerializable(typeof(EventIngestionV3Client))]
[JsonSerializable(typeof(EventIngestionV3User))]
[JsonSerializable(typeof(EventIngestionV3Request))]
[JsonSerializable(typeof(EventIngestionV3Environment))]
[JsonSerializable(typeof(EventIngestionV3Stacking))]
[JsonSerializable(typeof(EventIngestionV3Response))]
[JsonSerializable(typeof(EventIngestionV3Error))]
public sealed partial class EventIngestionJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    MaxDepth = EventIngestionV3Limits.MaximumJsonDepth,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = false)]
[JsonSerializable(typeof(EventIngestionV3SurvivorPayload))]
[JsonSerializable(typeof(EventIngestionV3SurvivorStacking))]
internal sealed partial class EventIngestionV3SurvivorJsonContext : JsonSerializerContext;
