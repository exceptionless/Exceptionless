namespace Exceptionless.Web.Models;

public record SignupModel : LoginModel
{
    public string? Name { get; set; }
}
