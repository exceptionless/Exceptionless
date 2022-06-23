using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using McSherry.SemanticVersioning;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Utility;

public class SemanticVersionParser {
    private static readonly IReadOnlyCollection<string> EmptyIdentifiers = new List<string>(0).AsReadOnly();
    private readonly ILogger _logger;

    public SemanticVersionParser(ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<SemanticVersionParser>();
    }

    public SemanticVersion Default { get; } = new SemanticVersion(0, 0);

    public SemanticVersion Parse(string version) {
        version = version?.Trim();
        if (String.IsNullOrEmpty(version))
            return null;

        int spaceIndex = version.IndexOf(" ", StringComparison.OrdinalIgnoreCase);
        if (spaceIndex > 0)
            version = version.Substring(0, spaceIndex).Trim();

        int wildCardIndex = version.IndexOf("*", StringComparison.OrdinalIgnoreCase);
        if (wildCardIndex > 0)
            version = version.Replace(".*", String.Empty).Replace("*", String.Empty);

        SemanticVersion semanticVersion = null;
        if (version.Length >= 5 && SemanticVersion.TryParse(version, ParseMode.Lenient, out semanticVersion))
            return semanticVersion;

        string[] versionParts = version.Split(new char[] { '+', ' ', '$', '-', '#', '=', '[', '@', '|', '/', '\\' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (versionParts.Length > 1 && SemanticVersion.TryParse(versionParts[0], ParseMode.Lenient, out semanticVersion))
            return semanticVersion;

        if (version.Length >= 3 && Version.TryParse(version, out var v))
            semanticVersion = new SemanticVersion(v.Major > 0 ? v.Major : 0, v.Minor > 0 ? v.Minor : 0, v.Build > 0 ? v.Build : 0, v.Revision >= 0 ? new[] { v.Revision.ToString() } : EmptyIdentifiers);
        else if (Int32.TryParse(version, out int major))
            semanticVersion = new SemanticVersion(major, 0);
        else
            _logger.LogInformation("Unable to parse version: {Version}", version);

        return semanticVersion;
    }
}
