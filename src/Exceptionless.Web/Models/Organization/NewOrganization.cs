using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record NewOrganization
{
    [Required]
    public string Name { get; set; } = null!;
}
