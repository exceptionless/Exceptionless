using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class TokenValidator : AbstractValidator<Token> {
        public TokenValidator() {
            RuleFor(t => t.Id).NotEmpty().WithMessage("Please specify a valid id.");
            RuleFor(t => t.OrganizationId).IsObjectId().When(t => !String.IsNullOrEmpty(t.OrganizationId)).WithMessage("Please specify a valid organization id.");
            RuleFor(t => t.OrganizationId).NotEmpty().When(t => !String.IsNullOrEmpty(t.ProjectId) || String.IsNullOrEmpty(t.UserId)).WithMessage("Please specify a valid organization id.");
            RuleFor(t => t.CreatedUtc).NotEmpty().WithMessage("Please specify a valid created date.");
            RuleFor(t => t.UpdatedUtc).NotEmpty().WithMessage("Please specify a valid updated date.");

            RuleFor(t => t.ProjectId).IsObjectId().When(t => !String.IsNullOrEmpty(t.ProjectId)).WithMessage("Please specify a valid project id.");
            RuleFor(t => t.DefaultProjectId).Must(String.IsNullOrEmpty).When(t => !String.IsNullOrEmpty(t.ProjectId)).WithMessage("Default project id cannot be set when a project id is defined.");
            RuleFor(t => t.DefaultProjectId).IsObjectId().When(t => !String.IsNullOrEmpty(t.DefaultProjectId)).WithMessage("Please specify a valid default project id.");
            RuleFor(t => t.UserId).Must(String.IsNullOrEmpty).When(t => !String.IsNullOrEmpty(t.ProjectId)).WithMessage("Can't set both user id and project id.");
            
            RuleFor(t => t.IsDisabled).Equal(false).When(t => t.Type != TokenType.Access).WithMessage("Only access tokens can be disabled");
        }
    }
}
