using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public sealed class AssistantOptions
{
    public const string DefaultEndpoint = "https://openrouter.ai/api/v1/chat/completions";
    public const string DefaultModel = "deepseek/deepseek-v4-flash";

    public bool Enabled { get; internal set; }
    public bool IsConfigured => !String.IsNullOrWhiteSpace(ApiKey);
    public bool IsAvailable => Enabled && IsConfigured;
    public string? ApiKey { get; internal set; }
    public string Endpoint { get; internal set; } = DefaultEndpoint;
    public string Model { get; internal set; } = DefaultModel;

    public static AssistantOptions ReadFromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Assistant");
        string? apiKey = section.GetValue<string>(nameof(ApiKey));
        return new AssistantOptions
        {
            Enabled = section.GetValue<bool?>(nameof(Enabled)) ?? !String.IsNullOrWhiteSpace(apiKey),
            ApiKey = apiKey,
            Endpoint = section.GetValue(nameof(Endpoint), DefaultEndpoint)!,
            Model = section.GetValue(nameof(Model), DefaultModel)!
        };
    }
}
