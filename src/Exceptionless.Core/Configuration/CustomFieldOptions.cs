using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class CustomFieldOptions
{
    public int MaxFieldsPerOrganization { get; internal set; }

    public static CustomFieldOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        return new CustomFieldOptions
        {
            MaxFieldsPerOrganization = config.GetValue(nameof(MaxFieldsPerOrganization), 20)
        };
    }
}
