using System;
using Exceptionless.Models.Admin;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class WebHookValidator : AbstractValidator<WebHook> {
        public WebHookValidator() {
            RuleFor(u => u.OrganizationId).NotEmpty().WithMessage("Please specify a valid organization id.");
            RuleFor(u => u.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
            RuleFor(u => u.Url).NotEmpty().WithMessage("Please specify a valid url.");
            RuleFor(u => u.EventTypes).NotEmpty().WithMessage("Please specify one or more event types.");
        }
    }
}