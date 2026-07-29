namespace Exceptionless.Core.Models;

/// <summary>
/// Per-column display settings for a saved view. All properties are optional so new settings can
/// be added without changing the meaning of existing saved views.
/// </summary>
public sealed record SavedViewColumnSettings
{
    public const int MaxPosition = 49;
    public const int MaxWidth = 1200;
    public const int MinWidth = 48;

    /// <summary>Whether the column is visible. Null means use the table default.</summary>
    public bool? Visible { get; set; }

    /// <summary>Zero-based display position. Null means use the table default order.</summary>
    public int? Position { get; set; }

    /// <summary>Column width in pixels. Null means use the table default width.</summary>
    public int? Width { get; set; }

}
