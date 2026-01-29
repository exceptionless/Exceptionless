using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record Signup : Login
{
    [Required]
    public string Name { get; init; } = null!;
}
