using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record SignupModel : LoginModel
{
    [Required]
    public string Name { get; init; } = null!;
}
