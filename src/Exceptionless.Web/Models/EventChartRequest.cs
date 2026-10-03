using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public sealed record EventChartRequest
{
    [Required]
    public EventChart Chart { get; set; } = null!;
    [MaxLength(2000)]
    public string? Filter { get; set; }
    [MaxLength(100)]
    public string? Time { get; set; }
    [MaxLength(20)]
    public string? Offset { get; set; }
}
