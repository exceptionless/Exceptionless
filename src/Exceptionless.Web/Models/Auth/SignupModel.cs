namespace Exceptionless.Web.Models;

public record SignupModel(string Email, string Password, string InviteToken, string Name)
    : LoginModel(Email, Password, InviteToken);
