using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class CustomFieldOptions
{
    public int MaxFieldsPerOrganization { get; internal set; }
    public int MaxLifetimeFieldsPerOrganization { get; internal set; }

    public static CustomFieldOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        int activeLimit = config.GetValue(nameof(MaxFieldsPerOrganization), 20);
        int lifetimeLimit = config.GetValue(nameof(MaxLifetimeFieldsPerOrganization), activeLimit);
        if (activeLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxFieldsPerOrganization), "Custom field active limit must be greater than zero.");
        if (lifetimeLimit < activeLimit)
            throw new ArgumentOutOfRangeException(nameof(MaxLifetimeFieldsPerOrganization), "Custom field lifetime limit must be greater than or equal to the active limit.");

        return new CustomFieldOptions
        {
            MaxFieldsPerOrganization = activeLimit,
            MaxLifetimeFieldsPerOrganization = lifetimeLimit
        };
    }
}
