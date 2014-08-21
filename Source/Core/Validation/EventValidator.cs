using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class EventValidator : AbstractValidator<Event> {
        public EventValidator() {
            RuleFor(e => e.Date).NotEmpty().WithMessage("Date must be specified.");
            RuleFor(e => e.Date).Must(date => date.UtcDateTime <= DateTime.UtcNow.AddHours(1)).WithMessage("Date cannot be in the future.");
            RuleFor(s => s.Type).NotEmpty().WithMessage("Type must be specified.");
            RuleFor(s => s.Type).Length(1, 100).WithMessage("Type cannot be longer than 100 characters.");
            RuleFor(s => s.Message).Length(0, 2000).WithMessage("Message cannot be longer than 2000 characters.");
            RuleFor(s => s.Source).Length(0, 2000).WithMessage("Source cannot be longer than 2000 characters.");
            
            RuleFor(e => e.ReferenceId).Length(8, 32).WithMessage("ReferenceId must contain between 8 and 32 characters");
            RuleForEach(s => s.Tags).NotEmpty().WithMessage("Tags can't be empty.");
            RuleForEach(e => e.Tags).Length(1, 255).WithMessage("A tag must be less than 255 characters.");
       }
    }
}