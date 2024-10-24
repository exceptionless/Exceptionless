using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record UpdateUser
{
    [Required]
    public string FullName { get; set; } = null!;
    public bool EmailNotificationsEnabled { get; set; }
}
