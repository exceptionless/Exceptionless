using System.Diagnostics;

namespace Exceptionless.Core.Models.Data;

[DebuggerDisplay("{Locality}, {Level2}, {Level1}, {Country}")]
public record Location
{
    public string? Country { get; init; }

    /// <summary>
    /// State / Province
    /// </summary>
    public string? Level1 { get; init; }

    /// <summary>
    /// County
    /// </summary>
    public string? Level2 { get; init; }

    /// <summary>
    /// City
    /// </summary>
    public string? Locality { get; init; }
}
