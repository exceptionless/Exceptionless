using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Web.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record PostProductTourActivity
{
    [Range(1, Int32.MaxValue)]
    public int Version { get; init; }

    [Required, EnumDataType(typeof(ProductTourTelemetryEvent))]
    public ProductTourTelemetryEvent? Action { get; init; }

    [Required, EnumDataType(typeof(ProductTourLaunchSource))]
    public ProductTourLaunchSource? Source { get; init; }

    [StringLength(64, MinimumLength = 1)]
    public string? Step { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateProductTourAnalytics
{
    [Required]
    public bool? Enabled { get; init; }
}
