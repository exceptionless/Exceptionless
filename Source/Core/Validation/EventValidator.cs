using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class EventValidator : AbstractValidator<Event> {
        public EventValidator() {
            RuleFor(e => e.Type)
                .NotEmpty()
                .Must(BeAValidEventType)
                .WithMessage("Please specify a valid event type.");
            
            RuleFor(e => e.ReferenceId)
                .NotEmpty()
                .Length(8, 32)
                .Unless(u => String.IsNullOrEmpty(u.ReferenceId))
                .WithMessage("ReferenceId must contain between 8 and 32 characters");
        }

        private bool BeAValidEventType(string type) {
            switch (type.ToLower()) {
                case Event.KnownTypes.Error:
                case Event.KnownTypes.NotFound:
                case Event.KnownTypes.Log:
                case Event.KnownTypes.FeatureUsage:
                case Event.KnownTypes.SessionStart:
                case Event.KnownTypes.SessionEnd:
                    break;
                default:
                    return false;
            }

            return true;
        }
    }
}