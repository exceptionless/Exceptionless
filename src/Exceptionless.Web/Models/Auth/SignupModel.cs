namespace Exceptionless.Web.Models;

public record SignupModel(string Name, string Email, string Password, string InviteToken = null)
    : LoginModel(Email, Password, InviteToken);
