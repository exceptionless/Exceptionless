using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Exceptionless.Api.Utility.Handlers {
    public class ApiException : Exception {
        public ApiException(string message, int statusCode = StatusCodes.Status500InternalServerError, ICollection<ApiErrorItem> errors = null) : base(message) {
            StatusCode = statusCode;
            Errors = errors;
        }

        public ApiException(Exception ex, int statusCode = StatusCodes.Status500InternalServerError) : base(ex.Message) {
            StatusCode = statusCode;
        }

        public int StatusCode { get; set; }
        public ICollection<ApiErrorItem> Errors { get; set; }
    }
}