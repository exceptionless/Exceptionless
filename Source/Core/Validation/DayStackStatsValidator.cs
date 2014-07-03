using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class DayStackStatsValidator : AbstractValidator<DayStackStats> {
        public DayStackStatsValidator() {
            RuleFor(d => d.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
            RuleFor(d => d.StackId).NotEmpty().WithMessage("Please specify a valid stack id.");
        }
    }
}