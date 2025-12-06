using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Operation transformer that reads XML documentation &lt;response&gt; tags
/// and adds them to OpenAPI operation responses.
/// </summary>
public class XmlDocumentationOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly Dictionary<string, XDocument> _xmlDocCache = new();
    private static readonly object _lock = new();

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var methodInfo = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .FirstOrDefault();

        if (methodInfo is null)
        {
            // For controller actions, try to get from ControllerActionDescriptor
            if (context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerDescriptor)
            {
                methodInfo = controllerDescriptor.MethodInfo;
            }
        }

        if (methodInfo is null)
        {
            return Task.CompletedTask;
        }

        var xmlDoc = GetXmlDocumentation(methodInfo.DeclaringType?.Assembly);
        if (xmlDoc is null)
        {
            return Task.CompletedTask;
        }

        var methodMemberName = GetMemberName(methodInfo);
        var memberElement = xmlDoc.Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == methodMemberName);

        if (memberElement is null)
        {
            return Task.CompletedTask;
        }

        var responseElements = memberElement.Elements("response");
        foreach (var responseElement in responseElements)
        {
            var codeAttribute = responseElement.Attribute("code");
            if (codeAttribute is null)
            {
                continue;
            }

            var statusCode = codeAttribute.Value;
            var description = responseElement.Value.Trim();

            // Skip if Responses is null or this response already exists
            if (operation.Responses is null || operation.Responses.ContainsKey(statusCode))
            {
                continue;
            }

            operation.Responses[statusCode] = new OpenApiResponse
            {
                Description = description
            };
        }

        return Task.CompletedTask;
    }

    private static XDocument? GetXmlDocumentation(Assembly? assembly)
    {
        if (assembly is null)
        {
            return null;
        }

        var assemblyName = assembly.GetName().Name;
        if (assemblyName is null)
        {
            return null;
        }

        lock (_lock)
        {
            if (_xmlDocCache.TryGetValue(assemblyName, out var cachedDoc))
            {
                return cachedDoc;
            }

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");
            if (!File.Exists(xmlPath))
            {
                return null;
            }

            try
            {
                var doc = XDocument.Load(xmlPath);
                _xmlDocCache[assemblyName] = doc;
                return doc;
            }
            catch
            {
                return null;
            }
        }
    }

    private static string GetMemberName(MethodInfo methodInfo)
    {
        var declaringType = methodInfo.DeclaringType;
        if (declaringType is null)
        {
            return string.Empty;
        }

        var typeName = declaringType.FullName?.Replace('+', '.');
        var parameters = methodInfo.GetParameters();

        if (parameters.Length == 0)
        {
            return $"M:{typeName}.{methodInfo.Name}";
        }

        var parameterTypes = string.Join(",", parameters.Select(p => GetParameterTypeName(p.ParameterType)));
        return $"M:{typeName}.{methodInfo.Name}({parameterTypes})";
    }

    private static string GetParameterTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeName = type.GetGenericTypeDefinition().FullName;
            if (genericTypeName is null)
            {
                return type.Name;
            }

            var backtickIndex = genericTypeName.IndexOf('`');
            if (backtickIndex > 0)
            {
                genericTypeName = genericTypeName[..backtickIndex];
            }

            var genericArgs = string.Join(",", type.GetGenericArguments().Select(GetParameterTypeName));
            return $"{genericTypeName}{{{genericArgs}}}";
        }

        if (type.IsArray)
        {
            return $"{GetParameterTypeName(type.GetElementType()!)}[]";
        }

        return type.FullName ?? type.Name;
    }
}
