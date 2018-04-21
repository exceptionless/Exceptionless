using System;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Web.Utility {
   public class FilterRoutesDocumentFilter : IDocumentFilter {
       public void Apply(SwaggerDocument swaggerDoc, DocumentFilterContext context) {
           swaggerDoc.Paths["/api/v{version}/error"].Post = null;
        }
   }
}