using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class MonthStackStatsValidator : AbstractValidator<MonthStackStats> {
        public MonthStackStatsValidator() {
            RuleFor(m => m.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
            RuleFor(m => m.StackId).NotEmpty().WithMessage("Please specify a valid stack id.");
        }
    }
}