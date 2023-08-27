using McSherry.SemanticVersioning;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Utility;

public class SemanticVersionParser
{
    private static readonly IReadOnlyCollection<string> EmptyIdentifiers = new List<string>(0).AsReadOnly();
    private readonly ILogger _logger;

    public SemanticVersionParser(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SemanticVersionParser>();
    }

    public SemanticVersion Default { get; } = new(0, 0);

    public SemanticVersion? Parse(string? version, IDictionary<string, SemanticVersion>? versionCache = null)
    {
        if (string.IsNullOrEmpty(version))
            return null;

        if (versionCache is not null && versionCache.TryGetValue(version, out var cachedVersion))
            return cachedVersion;

        int wildCardIndex = version.IndexOf('*');
        if (wildCardIndex > 0)
            version = version.Substring(0, wildCardIndex).TrimEnd('.');

        if (version.Length >= 5 && SemanticVersion.TryParse(version, out var semanticVersion))
        {
            if (versionCache is not null)
                versionCache[version] = semanticVersion;

            return semanticVersion;
        }

        string[] versionParts = version.Split(new char[] { '+', ' ', '$', '-', '#', '=', '[', ']', '(', ')', '@', '|', '/', '\\', '*' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (versionParts.Length > 1)
            version = versionParts[0];

        if (SemanticVersion.TryParse(version, ParseMode.Lenient, out semanticVersion))
        {
            if (versionCache is not null)
                versionCache[version] = semanticVersion;

            return semanticVersion;
        }

        if (version.Length >= 3 && Version.TryParse(version, out var v))
            semanticVersion = new SemanticVersion(v.Major > 0 ? v.Major : 0, v.Minor > 0 ? v.Minor : 0, v.Build > 0 ? v.Build : 0, v.Revision >= 0 ? new[] { v.Revision.ToString() } : EmptyIdentifiers);
        else if (Int32.TryParse(version, out int major))
            semanticVersion = new SemanticVersion(major, 0);
        else
            _logger.LogInformation("Unable to parse version: {Version}", version);

        if (versionCache is not null)
            versionCache[version] = semanticVersion;

        return semanticVersion;
    }
}
