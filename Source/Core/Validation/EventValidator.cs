using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class EventValidator : AbstractValidator<Event> {
        public EventValidator() {
            RuleFor(e => e.Date).NotEmpty().Must(date => date.UtcDateTime <= DateTime.UtcNow.AddHours(1)).WithMessage("Date cannot be in the future. ");
            RuleFor(s => s.Type).Length(1, 100).WithMessage("Type cannot be longer than 100 characters.");
            RuleFor(s => s.Message).Length(1, 2000).When(s => s.Message != null).WithMessage("Message cannot be longer than 2000 characters.");
            RuleFor(s => s.Source).Length(1, 2000).When(s => s.Source != null).WithMessage("Source cannot be longer than 2000 characters.");
    
            RuleFor(e => e.ReferenceId)
                .NotEmpty()
                .Length(8, 32)
                .Unless(u => String.IsNullOrEmpty(u.ReferenceId))
                .WithMessage("ReferenceId must contain between 8 and 32 characters");

            RuleForEach(e => e.Tags)
                .Length(1, 255)
                .WithMessage("A tag must be less than 255 characters.");
       }
    }
}