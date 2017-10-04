using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using FluentValidation;
using FluentValidation.Results;

namespace Exceptionless.Core.Validation {
    public class StackValidator : AbstractValidator<Stack> {
        public override ValidationResult Validate(ValidationContext<Stack> context) {
            var result = new ValidationResult();
            var stack = context.InstanceToValidate;

            if (!IsObjectId(stack.Id))
                result.Errors.Add(new ValidationFailure("Id", "Please specify a valid id."));

            if (!IsObjectId(stack.OrganizationId))
                result.Errors.Add(new ValidationFailure("OrganizationId", "Please specify a valid organization id."));

            if (!IsObjectId(stack.ProjectId))
                result.Errors.Add(new ValidationFailure("ProjectId", "Please specify a valid project id."));

            if (stack.Title != null && stack.Title.Length > 1000)
                result.Errors.Add(new ValidationFailure("Title", "Title cannot be longer than 1000 characters."));

            if (stack.Type != null && (stack.Type.Length < 1 || stack.Type.Length > 100))
                result.Errors.Add(new ValidationFailure("Type", "Type must be specified and cannot be longer than 100 characters."));

            // NOTE: We need to write a migration to cleanup all old stacks of 50 or more tags so there never is an error while saving.
            //if (stack.Tags.Count > 50)
            //    result.Errors.Add(new ValidationFailure("Tags", "Tags can't include more than 50 tags."));

            foreach (string tag in stack.Tags) {
                if (String.IsNullOrEmpty(tag))
                    result.Errors.Add(new ValidationFailure("Tags", "Tags can't be empty."));
                else if (tag.Length > 255)
                    result.Errors.Add(new ValidationFailure("Tags", "A tag cannot be longer than 255 characters."));
            }

            if (String.IsNullOrEmpty(stack.SignatureHash))
                result.Errors.Add(new ValidationFailure("SignatureHash", "Please specify a valid signature hash."));

            if (stack.SignatureInfo == null)
                result.Errors.Add(new ValidationFailure("SignatureInfo", "Please specify a valid signature info."));

            return result;
        }

        public override Task<ValidationResult> ValidateAsync(ValidationContext<Stack> context, CancellationToken cancellation = new CancellationToken()) {
            return Task.FromResult(Validate(context.InstanceToValidate));
        }

        private bool IsObjectId(string value) {
            if (String.IsNullOrEmpty(value))
                return false;

            return value.Length == 24;
        }
    }
}