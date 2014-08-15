using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class DayStackStatsValidator : AbstractValidator<DayStackStats> {
        public DayStackStatsValidator() {
            RuleFor(d => d.ProjectId).IsObjectId().WithMessage("Please specify a valid project id.");
            RuleFor(d => d.StackId).IsObjectId().WithMessage("Please specify a valid stack id.");
        }
    }
}