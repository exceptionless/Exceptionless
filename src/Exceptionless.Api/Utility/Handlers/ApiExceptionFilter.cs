using System;
using Exceptionless.Plugins;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Exceptionless.Api.Utility.Handlers {
    public class ApiExceptionFilter : ExceptionFilterAttribute {
        public override void OnException(ExceptionContext context) {
            var contextData = new ContextData();
            contextData.MarkAsUnhandledError();
            contextData.SetSubmissionMethod(nameof(ApiExceptionFilter));
            context.Exception.ToExceptionless(contextData).SetHttpContext(context.HttpContext).Submit();

            ApiError apiError;
            int statusCode = StatusCodes.Status500InternalServerError;

            if (context.Exception is ApiException apiException) {
                apiError = new ApiError(apiException.Message) {
                    Errors = apiException.Errors
                };

                statusCode = apiException.StatusCode;
            } else if (context.Exception is UnauthorizedAccessException unauthorizedAccessException) {
                apiError = new ApiError(unauthorizedAccessException.Message);
                statusCode = StatusCodes.Status401Unauthorized;
            } else if (context.Exception is ValidationException validationException) {
                apiError = new ApiError(validationException);
                statusCode = StatusCodes.Status400BadRequest;
            } else if (context.Exception is ApplicationException applicationException && applicationException.Message.Contains("version_conflict")) {
                apiError = new ApiError(applicationException.Message);
                statusCode = StatusCodes.Status400BadRequest;
            } else {
                apiError = new ApiError(context.Exception.GetBaseException().Message);

#if DEBUG
                apiError.Detail = context.Exception.StackTrace;
#endif
            }

            context.Result = new ObjectResult(apiError) {
                StatusCode = statusCode
            };

            base.OnException(context);
        }
    }
}