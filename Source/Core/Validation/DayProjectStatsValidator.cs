using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class DayProjectStatsValidator : AbstractValidator<DayProjectStats> {
        public DayProjectStatsValidator() {
            RuleFor(d => d.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
        }
    }
}