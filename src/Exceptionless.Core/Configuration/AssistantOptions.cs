using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public sealed class AssistantOptions
{
    public bool Enabled { get; internal set; }
    public bool IsConfigured => !String.IsNullOrWhiteSpace(ApiKey);
    public bool IsAvailable => Enabled && IsConfigured;
    public string? ApiKey { get; internal set; }
    public string Endpoint { get; internal set; } = "https://openrouter.ai/api/v1/chat/completions";
    public string Model { get; internal set; } = "deepseek/deepseek-v4-flash";

    public static AssistantOptions ReadFromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Assistant");
        string? apiKey = section.GetValue<string>(nameof(ApiKey));
        return new AssistantOptions
        {
            Enabled = section.GetValue<bool?>(nameof(Enabled)) ?? !String.IsNullOrWhiteSpace(apiKey),
            ApiKey = apiKey,
            Endpoint = section.GetValue(nameof(Endpoint), "https://openrouter.ai/api/v1/chat/completions")!,
            Model = section.GetValue(nameof(Model), "deepseek/deepseek-v4-flash")!
        };
    }
}
