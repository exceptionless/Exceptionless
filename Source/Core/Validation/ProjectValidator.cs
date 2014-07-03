using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class ProjectValidator : AbstractValidator<Project> {
        public ProjectValidator() {
            RuleFor(u => u.OrganizationId).NotEmpty().WithMessage("Please specify a valid organization id.");
            RuleFor(u => u.Name).NotEmpty().WithMessage("Please specify a valid name.");
            RuleFor(u => u.TimeZone).NotEmpty().WithMessage("Please specify a valid time zone.");
        }
    }
}