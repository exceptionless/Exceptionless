namespace Exceptionless.Web.Models.Admin;

public sealed record UpdateAssistantFullLoggingSettings
{
    public bool Enabled { get; init; }
}
