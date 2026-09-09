namespace Exceptionless.Web.Models.Admin;

public sealed record UpdateAssistantConversationSharingSettings
{
    public bool Enabled { get; init; }
}
