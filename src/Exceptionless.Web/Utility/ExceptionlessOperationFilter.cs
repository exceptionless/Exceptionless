using System;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Web.Utility {
   public class ExceptionlessOperationFilter : IOperationFilter {
        public void Apply(OpenApiOperation operation, OperationFilterContext context) { 
            operation.Consumes.Clear();
            operation.Consumes.Add("application/json");
            
            operation.Produces.Clear();
            operation.Produces.Add("application/json");
        }
    }
}