using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record UpdateOrganization
{
    [Required]
    public string Name { get; set; } = null!;

    public OrganizationBudgetAlertSettings? BudgetAlertSettings { get; set; }
}
