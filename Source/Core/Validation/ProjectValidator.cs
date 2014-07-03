using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class ProjectValidator : AbstractValidator<Project> {
        public ProjectValidator() {
            RuleFor(p => p.OrganizationId).NotEmpty().WithMessage("Please specify a valid organization id.");
            RuleFor(p => p.Name).NotEmpty().WithMessage("Please specify a valid name.");
            RuleFor(p => p.TimeZone).NotEmpty().WithMessage("Please specify a valid time zone.");
            RuleFor(p => p.NextSummaryEndOfDayTicks).NotEmpty().WithMessage("Please specify a valid next summary end of day ticks.");
        }
    }
}