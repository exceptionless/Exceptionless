using System;
using Exceptionless.Core.Jobs;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class JobLockInfoValidator : AbstractValidator<JobLockInfo> {
        public JobLockInfoValidator() {
            RuleFor(u => u.Name).NotEmpty().WithMessage("Please specify a valid name.");
            RuleFor(u => u.CreatedDate).NotEmpty().WithMessage("Please specify a valid created date.");
        }
    }
}