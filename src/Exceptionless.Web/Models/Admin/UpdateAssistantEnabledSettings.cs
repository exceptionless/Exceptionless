namespace Exceptionless.Web.Models.Admin;

public sealed record UpdateAssistantEnabledSettings
{
    public bool? Enabled { get; init; }
}
