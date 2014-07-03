using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class DayProjectStatsValidator : AbstractValidator<DayProjectStats> {
        public DayProjectStatsValidator() {
            RuleFor(u => u.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
        }
    }
}