using System;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Web.Utility {
   public class ExceptionlessOperationFilter : IOperationFilter {
        public void Apply(Operation operation, OperationFilterContext context) {
            operation.Consumes.Clear();
            operation.Consumes.Add("application/json");
            
            operation.Produces.Clear();
            operation.Produces.Add("application/json");
        }
    }
}