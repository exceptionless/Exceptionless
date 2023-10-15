using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record Signup : Login
{
    [Required]
    public required string Name { get; init; }
}
