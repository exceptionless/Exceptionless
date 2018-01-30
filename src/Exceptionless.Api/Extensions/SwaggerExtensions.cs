using System;
using System.Linq;
using System.Text.RegularExpressions;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Api.Extensions {
    public static class SwaggerExtensions {
        /// <summary>
        /// Matches /api/{version}/ url's (where {version} = v1 or v2) to their matching swagger specification, 
        /// and also resolves {version} parameters to absolute paths's for all versioned swagger specs
        /// </summary>
        public static void AddAutoVersioningSupport(this SwaggerGenOptions c) {
            c.DocInclusionPredicate((version, apiDescription) => {
                var regex = @"api\/(v\d)\/";
                var urlMatches = Regex.Match(apiDescription.RelativePath, regex);
                if (urlMatches.Groups.Count == 2) {
                    // will be 'v1', 'v2', or '{version}'
                    var urlVersion = urlMatches.Groups[1].Value.ToLower();
                    if (urlVersion == version)
                        return true;
                }
                return apiDescription.RelativePath.Contains("{apiVersion}");
            });
            c.OperationFilter<RemoveVersionParameters>();
            c.DocumentFilter<SetVersionInPaths>();
        }

        private class RemoveVersionParameters : IOperationFilter {
            public void Apply(Operation operation, OperationFilterContext context) {
                var versionParameter = operation.Parameters?.SingleOrDefault(p => p.Name == "apiVersion");
                if (versionParameter != null)
                    operation.Parameters.Remove(versionParameter);
            }
        }

        private class SetVersionInPaths : IDocumentFilter {
            public void Apply(SwaggerDocument doc, DocumentFilterContext context) {
                foreach (var item in doc.Paths.Where(kvp => kvp.Key.Contains("{apiVersion}")).ToArray()) {
                    doc.Paths.Remove(item.Key);
                    if (doc.Info.Version == "v1")
                        continue;

                    var key = item.Key.Replace("v{apiVersion}", doc.Info.Version);
                    var toAdd = item.Value;

                    var operations = typeof(PathItem)
                        .GetProperties()
                        .Where(p => p.PropertyType == typeof(Operation))
                        .ToDictionary(p => p, p => (Operation)p.GetValue(toAdd))
                        .Where(kvp => kvp.Value != null)
                        .ToList();

                    operations.ForEach(o => o.Value.OperationId = o.Value.OperationId.Replace("V{apiVersion", doc.Info.Version.ToUpper()));

                    if (!doc.Paths.ContainsKey(key)) {
                        doc.Paths[key] = toAdd;
                        continue;
                    }

                    Operation UpsertOperation(string v, Operation existing, Operation current) {
                        if (existing != null && current != null)
                            throw new InvalidOperationException($"Two operations with the same path ({key}) and verb ({v}) is not supported.");

                        return existing ?? current;
                    }

                    var toUpdate = doc.Paths[key];
                    toUpdate.Delete = UpsertOperation("Delete", toUpdate.Delete, toAdd.Delete);
                    toUpdate.Put = UpsertOperation("Put", toUpdate.Put, toAdd.Put);
                    toUpdate.Get = UpsertOperation("Get", toUpdate.Get, toAdd.Get);
                    toUpdate.Head = UpsertOperation("Head", toUpdate.Head, toAdd.Head);
                    toUpdate.Options = UpsertOperation("Options", toUpdate.Options, toAdd.Options);
                    toUpdate.Patch = UpsertOperation("Patch", toUpdate.Patch, toAdd.Patch);
                    toUpdate.Post = UpsertOperation("Post", toUpdate.Post, toAdd.Post);
                }
            }
        }
    }
}
