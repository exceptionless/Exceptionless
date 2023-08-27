using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exceptionless.Web.Utility.Handlers;

public record ApiError
{
    public string? Message { get; set; }
    public string? ReferenceId { get; set; }
    public bool IsError => true;
    public string? Detail { get; set; }
    public ICollection<ApiErrorItem>? Errors { get; set; }

    public ApiError(string message, string referenceId)
    {
        Message = message;
        ReferenceId = referenceId;
        Errors = new List<ApiErrorItem>();
    }

    public ApiError(ModelStateDictionary modelState)
    {
        if (modelState.Any(m => m.Value is { Errors.Count: > 0 }))
        {
            Message = "Please correct the specified errors and try again.";
            //errors = modelState.SelectMany(m => m.Value.Errors).ToDictionary(m => m.Key, m=> m.ErrorMessage);
            //errors = modelState.SelectMany(m => m.Value.Errors.Select( me => new KeyValuePair<string,string>( m.Key,me.ErrorMessage) ));
            //errors = modelState.SelectMany(m => m.Value.Errors.Select(me => new ModelError { FieldName = m.Key, ErrorMessage = me.ErrorMessage }));
        }
    }

    public ApiError(ValidationException ex, string referenceId)
    {
        Message = "Please correct the specified errors and try again.";
        ReferenceId = referenceId;
        Errors = ex.Errors.Select(error => new ApiErrorItem
        {
            PropertyName = error.PropertyName,
            Message = error.ErrorMessage,
            AttemptedValue = error.AttemptedValue
        }).ToList();
    }
}

public record ApiErrorItem
{
    public required string PropertyName { get; set; }
    public required string Message { get; set; }
    public required object AttemptedValue { get; set; }
}
