using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exceptionless.Web.Utility.Handlers {
    public class ApiError {
        public string Message { get; set; }
        public string ReferenceId { get; set; }
        public bool IsError => true;
        public string Detail { get; set; }
        public ICollection<ApiErrorItem> Errors { get; set; }

        public ApiError(string message, string referenceId) {
            Message = message;
            ReferenceId = referenceId;
            Errors = new List<ApiErrorItem>();
        }


        public ApiError(ModelStateDictionary modelState) {
            if (modelState != null && modelState.Any(m => m.Value.Errors.Count > 0)) {
                Message = "Please correct the specified errors and try again.";
                //errors = modelState.SelectMany(m => m.Value.Errors).ToDictionary(m => m.Key, m=> m.ErrorMessage);
                //errors = modelState.SelectMany(m => m.Value.Errors.Select( me => new KeyValuePair<string,string>( m.Key,me.ErrorMessage) ));
                //errors = modelState.SelectMany(m => m.Value.Errors.Select(me => new ModelError { FieldName = m.Key, ErrorMessage = me.ErrorMessage }));
            }
        }
        public ApiError(ValidationException ex, string referenceId) {
            Message = "Please correct the specified errors and try again.";
            ReferenceId = referenceId;
            Errors = ex.Errors.Select(error => new ApiErrorItem {
                PropertyName = error.PropertyName,
                Message = error.ErrorMessage,
                AttemptedValue = error.AttemptedValue
            }).ToList();
        }
    }

    public class ApiErrorItem {
        public string PropertyName { get; set; }
        public string Message { get; set; }
        public object AttemptedValue { get; set; }
    }
}
