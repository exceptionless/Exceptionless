using System;
using System.Web.Http.Description;
using Swashbuckle.Swagger;

namespace Exceptionless.Api.Utility {
   public class FilterRoutesDocumentFilter : IDocumentFilter {
        public void Apply(SwaggerDocument swaggerDoc, SchemaRegistry schemaRegistry, IApiExplorer apiExplorer) {
            swaggerDoc.paths["/api/v{version}/error"].post = null;
        }
    }
}