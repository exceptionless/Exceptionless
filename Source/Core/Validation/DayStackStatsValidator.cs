using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class DayStackStatsValidator : AbstractValidator<DayStackStats> {
        public DayStackStatsValidator() {
            RuleFor(u => u.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
            RuleFor(u => u.StackId).NotEmpty().WithMessage("Please specify a valid stack id.");
        }
    }
}