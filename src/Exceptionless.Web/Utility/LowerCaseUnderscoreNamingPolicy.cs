using System.Text.Json;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Web.Utility;

/// <summary>
/// A JSON naming policy that converts PascalCase to lower_case_underscore format.
/// This uses the existing ToLowerUnderscoredWords extension method to maintain
/// API compatibility with legacy Newtonsoft.Json serialization.
///
/// Note: This implementation treats each uppercase letter individually, so:
/// - "OSName" becomes "o_s_name" (not "os_name")
/// - "EnableSSL" becomes "enable_s_s_l" (not "enable_ssl")
/// - "BaseURL" becomes "base_u_r_l" (not "base_url")
/// - "PropertyName" becomes "property_name"
///
/// This matches the legacy behavior. See https://github.com/exceptionless/Exceptionless.Net/issues/2
/// for discussion on future improvements.
/// </summary>
public sealed class LowerCaseUnderscoreNamingPolicy : JsonNamingPolicy
{
    public static LowerCaseUnderscoreNamingPolicy Instance { get; } = new();

    public override string ConvertName(string name)
    {
        return name.ToLowerUnderscoredWords();
    }
}
