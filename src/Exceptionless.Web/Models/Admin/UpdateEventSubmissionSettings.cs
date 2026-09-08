namespace Exceptionless.Web.Models.Admin;

public sealed record UpdateEventSubmissionSettings
{
    public bool? Enabled { get; init; }
}

public sealed record EventSubmissionSettings(bool Enabled, bool ConfiguredEnabled, bool IsOverridden);
