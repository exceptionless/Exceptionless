using System;
using Exceptionless.Core.Billing;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class OrganizationValidator : AbstractValidator<Organization> {
        public OrganizationValidator() {
            RuleFor(o => o.Name).NotEmpty().WithMessage("Please specify a valid name.");
            RuleFor(o => o.PlanId).NotEmpty().WithMessage("Please specify a valid plan id.");
            RuleFor(o => o.HasPremiumFeatures).Equal(false).When(o => o.PlanId == BillingManager.FreePlan.Id).WithMessage("Please specify a valid plan id.");

            RuleFor(o => o.SuspensionCode).NotEmpty().When(o => o.IsSuspended).WithMessage("Please specify a valid suspension code.");
            RuleFor(o => o.SuspensionCode).Equal((SuspensionCode?)null).Unless(o => o.IsSuspended).WithMessage("The suspension code cannot be set while an organization is not suspended.");
            RuleFor(o => o.SuspensionDate).NotEmpty().When(o => o.IsSuspended).WithMessage("Please specify a valid suspension date.");
            RuleFor(o => o.SuspensionDate).Equal((DateTime?)null).Unless(o => o.IsSuspended).WithMessage("The suspension date cannot be set while an organization is not suspended.");
            RuleFor(o => o.SuspendedByUserId).NotEmpty().When(o => o.IsSuspended).WithMessage("Please specify a user id of user that suspended this organization.");
            RuleFor(o => o.SuspendedByUserId).Equal((string)null).Unless(o => o.IsSuspended).WithMessage("The suspended by user id cannot be set while an organization is not suspended.");
        }
    }
}