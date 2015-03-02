using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using FluentValidation;
using FluentValidation.Results;

namespace Exceptionless.Core.Validation {
    public class PersistentEventValidator : AbstractValidator<PersistentEvent> {
        public override ValidationResult Validate(PersistentEvent ev) {
            var result = new ValidationResult();

            if (!IsObjectId(ev.Id))
                result.Errors.Add(new ValidationFailure("Id", "Please specify a valid id."));

            if (!IsObjectId(ev.OrganizationId))
                result.Errors.Add(new ValidationFailure("OrganizationId", "Please specify a valid organization id."));

            if (!IsObjectId(ev.ProjectId))
                result.Errors.Add(new ValidationFailure("ProjectId", "Please specify a valid project id."));

            if (!IsObjectId(ev.StackId))
                result.Errors.Add(new ValidationFailure("StackId", "Please specify a valid stack id."));

            if (ev.Date == DateTimeOffset.MinValue)
                result.Errors.Add(new ValidationFailure("Date", "Date must be specified."));

            if (ev.Date.UtcDateTime > DateTime.UtcNow.AddHours(1))
                result.Errors.Add(new ValidationFailure("Date", "Date cannot be in the future."));

            if (String.IsNullOrEmpty(ev.Type) || ev.Type.Length > 100)
                result.Errors.Add(new ValidationFailure("Type", "Type cannot be longer than 100 characters."));

            if (ev.Message != null && (ev.Message.Length < 1 || ev.Message.Length > 2000))
                result.Errors.Add(new ValidationFailure("Message", "Message cannot be longer than 2000 characters."));

            if (ev.Source != null && (ev.Source.Length < 1 || ev.Source.Length > 2000))
                result.Errors.Add(new ValidationFailure("Source", "Source cannot be longer than 2000 characters."));

            if (!IsValidIdentifier(ev.ReferenceId))
                result.Errors.Add(new ValidationFailure("ReferenceId", "ReferenceId must contain between 8 and 100 alphanumeric or '-' characters."));

            if (!IsValidIdentifier(ev.SessionId))
                result.Errors.Add(new ValidationFailure("SessionId", "SessionId must contain between 8 and 100 alphanumeric or '-' characters."));

            foreach (var tag in ev.Tags) {
                if (String.IsNullOrEmpty(tag))
                    result.Errors.Add(new ValidationFailure("Tags", "Tags can't be empty."));
                else if (tag.Length > 255)
                    result.Errors.Add(new ValidationFailure("Tags", "A tag cannot be longer than 255 characters."));
            }

            return result;
        }

        private bool IsValidIdentifier(string value) {
            if (value == null)
                return true;

            if (value.Length < 8 || value.Length > 100)
                return false;

            return value.IsValidIdentifier();
        }

        private bool IsObjectId(string value) {
            if (String.IsNullOrEmpty(value))
                return false;

            return value.Length == 24;
        }
    }
}