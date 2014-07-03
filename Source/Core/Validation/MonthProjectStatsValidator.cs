using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class MonthProjectStatsValidator : AbstractValidator<MonthProjectStats> {
        public MonthProjectStatsValidator() {
            RuleFor(m => m.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
        }
    }
}