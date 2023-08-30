using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record TokenResult
{
    [Required]
    public required string Token { get; init; }
}
