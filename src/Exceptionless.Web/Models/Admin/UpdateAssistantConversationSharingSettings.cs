namespace Exceptionless.Web.Models.Admin;

public sealed record UpdateAssistantConversationSharingSettings
{
    public required bool Enabled { get; init; }
}
