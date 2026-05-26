using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ControllerManifestTests
{
    [Fact]
    public async Task GetControllerManifest_AllEndpoints_ReturnsExpectedBaseline()
    {
        // Arrange
        string baselinePath = Path.Combine(AppContext.BaseDirectory, "Controllers", "Data", "controller-manifest.json");

        // Act
        string actualJson = BuildManifestJson();

        // Set UPDATE_SNAPSHOTS=true to regenerate the baseline file.
        if (String.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(baselinePath, actualJson, TestContext.Current.CancellationToken);
            return;
        }

        // Assert
        string expectedJson = (await File.ReadAllTextAsync(baselinePath, TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        actualJson = actualJson.Replace("\r\n", "\n");
        Assert.Equal(expectedJson, actualJson);
    }

    internal static string BuildManifestJson()
    {
        var manifest = GetEndpoints()
            .OrderBy(endpoint => endpoint.Route, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.HttpMethod, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Controller, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Action, StringComparer.Ordinal)
            .ToArray();

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        });
    }

    private static IEnumerable<ControllerEndpointManifest> GetEndpoints()
    {
        var controllerTypes = typeof(AuthController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => type.Namespace is not null
                && (type.Namespace.StartsWith("Exceptionless.Web.Controllers", StringComparison.Ordinal)
                    || type.Namespace.StartsWith("Exceptionless.App.Controllers", StringComparison.Ordinal)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var controllerType in controllerTypes)
        {
            var controllerRoutes = controllerType.GetCustomAttributes<RouteAttribute>(true)
                .Select(attribute => attribute.Template)
                .DefaultIfEmpty(null)
                .ToArray();
            var controllerAttributes = controllerType.GetCustomAttributes(true).ToArray();

            foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                         .Where(method => !method.IsSpecialName)
                         .Where(method => !method.GetCustomAttributes<NonActionAttribute>(true).Any())
                         .OrderBy(method => method.Name, StringComparer.Ordinal))
            {
                var httpAttributes = method.GetCustomAttributes<HttpMethodAttribute>(true).ToArray();
                if (httpAttributes.Length == 0)
                    continue;

                var methodRouteAttributes = method.GetCustomAttributes(true)
                    .OfType<RouteAttribute>()
                    .Where(attribute => attribute.GetType() == typeof(RouteAttribute))
                    .ToArray();
                var methodAttributes = method.GetCustomAttributes(true).ToArray();

                foreach (var controllerRoute in controllerRoutes)
                {
                    foreach (var httpAttribute in httpAttributes)
                    {
                        var routeTemplates = ResolveMethodRouteTemplates(httpAttribute, methodRouteAttributes);
                        string? routeName = httpAttribute.Name ?? methodRouteAttributes.FirstOrDefault()?.Name;

                        foreach (var httpMethod in httpAttribute.HttpMethods.OrderBy(value => value, StringComparer.Ordinal))
                        {
                            foreach (var routeTemplate in routeTemplates)
                            {
                                yield return new ControllerEndpointManifest
                                {
                                    Controller = controllerType.Name,
                                    Action = method.Name,
                                    HttpMethod = httpMethod,
                                    Route = CombineRouteTemplates(controllerRoute, routeTemplate),
                                    Name = routeName,
                                    Authorization = GetAuthorizationAttributes(controllerAttributes, methodAttributes),
                                    Consumes = GetContentTypes<ConsumesAttribute>(controllerAttributes, methodAttributes),
                                    Produces = GetContentTypes<ProducesAttribute>(controllerAttributes, methodAttributes),
                                    Obsolete = methodAttributes.OfType<ObsoleteAttribute>().Select(attribute => attribute.Message).FirstOrDefault()
                                        ?? controllerAttributes.OfType<ObsoleteAttribute>().Select(attribute => attribute.Message).FirstOrDefault(),
                                    ExcludeFromDescription = IsExcludedFromDescription(controllerAttributes, methodAttributes)
                                };
                            }
                        }
                    }
                }
            }
        }
    }

    private static string[] ResolveMethodRouteTemplates(HttpMethodAttribute httpAttribute, RouteAttribute[] methodRouteAttributes)
    {
        if (!String.IsNullOrEmpty(httpAttribute.Template))
            return [httpAttribute.Template];

        if (methodRouteAttributes.Length > 0)
            return methodRouteAttributes.Select(attribute => attribute.Template ?? String.Empty).ToArray();

        return [String.Empty];
    }

    private static string CombineRouteTemplates(string? controllerTemplate, string? methodTemplate)
    {
        if (IsAbsoluteTemplate(methodTemplate))
            return NormalizeRoute(methodTemplate!);

        if (String.IsNullOrEmpty(controllerTemplate))
            return NormalizeRoute(methodTemplate ?? String.Empty);

        if (String.IsNullOrEmpty(methodTemplate))
            return NormalizeRoute(controllerTemplate);

        return NormalizeRoute($"{controllerTemplate.TrimEnd('/')}/{methodTemplate.TrimStart('/')}");
    }

    private static bool IsAbsoluteTemplate(string? template)
    {
        return !String.IsNullOrEmpty(template) && (template.StartsWith("~/", StringComparison.Ordinal) || template.StartsWith("/", StringComparison.Ordinal));
    }

    private static string NormalizeRoute(string route)
    {
        route = route.Trim();
        if (route.StartsWith("~/", StringComparison.Ordinal))
            route = route[1..];
        else if (!route.StartsWith("/", StringComparison.Ordinal))
            route = "/" + route;

        if (route.Length > 1)
            route = route.TrimEnd('/');

        return route;
    }

    private static string[] GetAuthorizationAttributes(object[] controllerAttributes, object[] methodAttributes)
    {
        return controllerAttributes.Concat(methodAttributes)
            .Where(attribute => attribute is AuthorizeAttribute or AllowAnonymousAttribute)
            .Select(DescribeAuthorizationAttribute)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string DescribeAuthorizationAttribute(object attribute)
    {
        if (attribute is AllowAnonymousAttribute)
            return nameof(AllowAnonymousAttribute).Replace("Attribute", String.Empty, StringComparison.Ordinal);

        var authorize = (AuthorizeAttribute)attribute;
        var segments = new List<string>();
        if (!String.IsNullOrWhiteSpace(authorize.Policy))
            segments.Add($"Policy={authorize.Policy}");
        if (!String.IsNullOrWhiteSpace(authorize.Roles))
            segments.Add($"Roles={authorize.Roles}");
        if (!String.IsNullOrWhiteSpace(authorize.AuthenticationSchemes))
            segments.Add($"AuthenticationSchemes={authorize.AuthenticationSchemes}");

        return segments.Count == 0 ? "Authorize" : $"Authorize({String.Join(", ", segments)})";
    }

    private static string[] GetContentTypes<TAttribute>(object[] controllerAttributes, object[] methodAttributes) where TAttribute : Attribute
    {
        return controllerAttributes.Concat(methodAttributes)
            .OfType<TAttribute>()
            .SelectMany(attribute => attribute switch
            {
                ConsumesAttribute consumes => consumes.ContentTypes,
                ProducesAttribute produces => produces.ContentTypes,
                _ => []
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsExcludedFromDescription(object[] controllerAttributes, object[] methodAttributes)
    {
        return controllerAttributes.Concat(methodAttributes).Any(attribute =>
            attribute.GetType().Name == "ExcludeFromDescriptionAttribute"
            || attribute is ApiExplorerSettingsAttribute { IgnoreApi: true });
    }

    private sealed record ControllerEndpointManifest
    {
        public required string Controller { get; init; }
        public required string Action { get; init; }
        public required string HttpMethod { get; init; }
        public required string Route { get; init; }
        public string? Name { get; init; }
        public required string[] Authorization { get; init; }
        public required string[] Consumes { get; init; }
        public required string[] Produces { get; init; }
        public string? Obsolete { get; init; }
        public bool ExcludeFromDescription { get; init; }
    }
}
