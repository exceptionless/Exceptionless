using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class TokenValidator : AbstractValidator<Token> {
        public TokenValidator() {
            RuleFor(t => t.Id).NotEmpty().WithMessage("Please specify a valid id.");
            RuleFor(t => t.OrganizationId).IsObjectId().When(p => !String.IsNullOrEmpty(p.OrganizationId)).WithMessage("Please specify a valid organization id.");
            RuleFor(t => t.OrganizationId).NotEmpty().When(p => !String.IsNullOrEmpty(p.ProjectId) || String.IsNullOrEmpty(p.UserId)).WithMessage("Please specify a valid organization id.");
            RuleFor(t => t.CreatedUtc).NotEmpty().WithMessage("Please specify a valid created date.");
            RuleFor(t => t.UpdatedUtc).NotEmpty().WithMessage("Please specify a valid updated date.");

            RuleFor(t => t.ProjectId).IsObjectId().When(p => !String.IsNullOrEmpty(p.ProjectId)).WithMessage("Please specify a valid project id.");
            RuleFor(t => t.DefaultProjectId).Must(String.IsNullOrEmpty).When(p => !String.IsNullOrEmpty(p.ProjectId)).WithMessage("Default project id cannot be set when a project id is defined.");
            RuleFor(t => t.DefaultProjectId).IsObjectId().When(p => !String.IsNullOrEmpty(p.DefaultProjectId)).WithMessage("Please specify a valid default project id.");
            RuleFor(t => t.UserId).Must(String.IsNullOrEmpty).When(p => !String.IsNullOrEmpty(p.ProjectId)).WithMessage("Can't set both user id and project id.");
        }
    }
}
