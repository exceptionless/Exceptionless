using FluentValidation;

namespace Exceptionless.Web.Models;

public record LoginModel(string Email, string Password, string InviteToken = null);

public class LoginModelValidator : AbstractValidator<LoginModel>
{
    public LoginModelValidator()
    {
        RuleFor(u => u.Email).NotEmpty().EmailAddress().WithMessage("Please specify a valid email address.");
        RuleFor(u => u.Password).MinimumLength(6).WithMessage("Please specify a valid password.");
        RuleFor(u => u.InviteToken).Length(40).When(m => m.InviteToken is not null).WithMessage("Please specify a valid invite token.");
    }
}
