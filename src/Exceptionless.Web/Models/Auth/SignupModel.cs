using FluentValidation;

namespace Exceptionless.Web.Models;

public record SignupModel(string Name, string Email, string Password, string? InviteToken = null) : LoginModel(Email, Password, InviteToken);

public class SignupModelValidator : AbstractValidator<SignupModel>
{
    public SignupModelValidator()
    {
        RuleFor(u => u.Name).NotEmpty();
        RuleFor(u => u.Email).NotEmpty().EmailAddress();
        RuleFor(u => u.Password).NotEmpty().MinimumLength(6).MaximumLength(100);
        RuleFor(u => u.InviteToken).NotEmpty().Length(40).When(m => m.InviteToken is not null);
    }
}
