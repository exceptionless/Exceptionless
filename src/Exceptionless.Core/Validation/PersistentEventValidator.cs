using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using FluentValidation;
using FluentValidation.Results;
using Foundatio.Utility;

namespace Exceptionless.Core.Validation {
    public class PersistentEventValidator : AbstractValidator<PersistentEvent> {
        public override ValidationResult Validate(ValidationContext<PersistentEvent> context) {
            var result = new ValidationResult();
            var ev = context.InstanceToValidate;

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

            if (ev.Date.UtcDateTime > SystemClock.UtcNow.AddHours(1))
                result.Errors.Add(new ValidationFailure("Date", "Date cannot be in the future."));

            if (String.IsNullOrEmpty(ev.Type))
                result.Errors.Add(new ValidationFailure("Type", "Type must be specified"));
            else if (ev.Type.Length > 100)
                result.Errors.Add(new ValidationFailure("Type", "Type cannot be longer than 100 characters."));

            if (ev.Message != null && (ev.Message.Length < 1 || ev.Message.Length > 2000))
                result.Errors.Add(new ValidationFailure("Message", "Message cannot be longer than 2000 characters."));

            if (ev.Source != null && (ev.Source.Length < 1 || ev.Source.Length > 2000))
                result.Errors.Add(new ValidationFailure("Source", "Source cannot be longer than 2000 characters."));

            if (!ev.HasValidReferenceId())
                result.Errors.Add(new ValidationFailure("ReferenceId", "ReferenceId must contain between 8 and 100 alphanumeric or '-' characters."));

            // NOTE: We need to write a migration to cleanup all old events of 50 or more tags so there never is an error while saving.
            //if (ev.Tags.Count > 50)
            //    result.Errors.Add(new ValidationFailure("Tags", "Tags can't include more than 50 tags."));

            foreach (string tag in ev.Tags) {
                if (String.IsNullOrEmpty(tag))
                    result.Errors.Add(new ValidationFailure("Tags", "Tags can't be empty."));
                else if (tag.Length > 255)
                    result.Errors.Add(new ValidationFailure("Tags", "A tag cannot be longer than 255 characters."));
            }

            return result;
        }

        public override Task<ValidationResult> ValidateAsync(ValidationContext<PersistentEvent> context, CancellationToken cancellation = new CancellationToken()) {
            return Task.FromResult(Validate(context.InstanceToValidate));
        }

        private bool IsObjectId(string value) {
            if (String.IsNullOrEmpty(value))
                return false;

            return value.Length == 24;
        }
    }
}