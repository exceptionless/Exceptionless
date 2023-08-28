﻿using Exceptionless.Plugins;
using FluentValidation;
using Foundatio.Repositories.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Exceptionless.Web.Utility.Handlers;

public class ApiExceptionFilter : ExceptionFilterAttribute
{
    private readonly ILogger _logger;

    public ApiExceptionFilter(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ApiExceptionFilter>();
    }

    public override void OnException(ExceptionContext context)
    {
        var contextData = new ContextData();
        contextData.MarkAsUnhandledError();
        contextData.SetSubmissionMethod(nameof(ApiExceptionFilter));
        var builder = context.Exception.ToExceptionless(contextData).SetHttpContext(context.HttpContext);
        builder.Submit();

        // TODO: pull the reference id using the reference id manager.
        string referenceId = builder.Target.ReferenceId;
        using var _ = _logger.BeginScope(new ExceptionlessState().Property("Reference", referenceId));
        _logger.LogError(context.Exception, "Unhandled error: {Message}", context.Exception.Message);

        ApiError apiError;
        int statusCode = StatusCodes.Status500InternalServerError;

        if (context.Exception is ApiException apiException)
        {
            apiError = new ApiError(apiException.Message, referenceId)
            {
                Errors = apiException.Errors
            };

            statusCode = apiException.StatusCode;
        }
        else if (context.Exception is UnauthorizedAccessException unauthorizedAccessException)
        {
            apiError = new ApiError(unauthorizedAccessException.Message, referenceId);
            statusCode = StatusCodes.Status401Unauthorized;
        }
        else if (context.Exception is NotImplementedException notImplementedException)
        {
            apiError = new ApiError(notImplementedException.Message, referenceId);
            statusCode = StatusCodes.Status501NotImplemented;
        }
        else if (context.Exception is ValidationException validationException)
        {
            apiError = new ApiError(validationException, referenceId);
            statusCode = StatusCodes.Status422UnprocessableEntity;
        }
        else if (context.Exception is ApplicationException applicationException && applicationException.Message.Contains("version_conflict"))
        {
            apiError = new ApiError(applicationException.Message, referenceId);
            statusCode = StatusCodes.Status409Conflict;
        }
        else if (context.Exception is VersionConflictDocumentException versionConflictDocumentException)
        {
            apiError = new ApiError(versionConflictDocumentException.Message, referenceId);
            statusCode = StatusCodes.Status409Conflict;
        }
        else
        {

#if DEBUG
            apiError = new ApiError(context.Exception.GetBaseException().Message, referenceId)
            {
                Detail = context.Exception.StackTrace
            };
#else
            apiError = new ApiError("An error occurred while serving your request.", referenceId);
#endif
        }

        context.Result = new ObjectResult(apiError)
        {
            StatusCode = statusCode
        };

        base.OnException(context);
    }
}
