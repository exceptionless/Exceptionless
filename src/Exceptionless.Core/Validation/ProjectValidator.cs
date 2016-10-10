using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class ProjectValidator : AbstractValidator<Project> {
        public ProjectValidator() {
            RuleFor(p => p.OrganizationId).IsObjectId().WithMessage("Please specify a valid organization id.");
            RuleFor(p => p.Name).NotEmpty().WithMessage("Please specify a valid name.");
            RuleFor(p => p.NextSummaryEndOfDayTicks).NotEmpty().WithMessage("Please specify a valid next summary end of day ticks.");
        }
    }
}