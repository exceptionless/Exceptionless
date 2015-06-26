using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class WebHookValidator : AbstractValidator<WebHook> {
        public WebHookValidator() {
            RuleFor(w => w.OrganizationId).IsObjectId().WithMessage("Please specify a valid organization id.");
            RuleFor(w => w.ProjectId).IsObjectId().When(p => String.IsNullOrEmpty(p.OrganizationId)).WithMessage("Please specify a valid project id.");
            RuleFor(w => w.Url).NotEmpty().WithMessage("Please specify a valid url.");
            RuleFor(w => w.EventTypes).NotEmpty().WithMessage("Please specify one or more event types.");
            RuleFor(w => w.Version).NotEmpty().WithMessage("Please specify a valid version.");
        }
    }
}