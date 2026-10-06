using System.Text.Json.Serialization;

namespace Exceptionless.EmailTemplates.Components;

internal sealed record EmailMessageMetadata(
    [property: JsonPropertyName("@context")] string Context,
    [property: JsonPropertyName("@type")] string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("potentialAction")] EmailViewActionMetadata PotentialAction,
    [property: JsonPropertyName("publisher")] EmailPublisherMetadata Publisher);

internal sealed record EmailViewActionMetadata(
    [property: JsonPropertyName("@type")] string Type,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("name")] string Name);

internal sealed record EmailPublisherMetadata(
    [property: JsonPropertyName("@type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("logo")] string Logo);

[JsonSerializable(typeof(EmailMessageMetadata), GenerationMode = JsonSourceGenerationMode.Serialization)]
internal sealed partial class EmailTemplateJsonSerializerContext : JsonSerializerContext;
