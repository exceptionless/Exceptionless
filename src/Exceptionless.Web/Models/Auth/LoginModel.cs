using FluentValidation;

namespace Exceptionless.Web.Models;

public record LoginModel(string Email, string Password, string? InviteToken = null);

public class LoginModelValidator : AbstractValidator<LoginModel>
{
    public LoginModelValidator()
    {
        RuleFor(u => u.Email).NotEmpty().EmailAddress();
        RuleFor(u => u.Password).NotEmpty().MinimumLength(6).MaximumLength(100);
        RuleFor(u => u.InviteToken).NotEmpty().Length(40).When(m => m.InviteToken is not null);
    }
}
