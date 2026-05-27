using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class EndpointManifestTests
{
    [Fact]
    public Task MapApiEndpoints_DefaultServices_MatchesSnapshot()
    {
        // Arrange
        using var app = MinimalApiTestApp.Create();

        // Act
        var manifest = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(CreateManifestEntries)
            .OrderBy(endpoint => endpoint.Route, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.DisplayName, StringComparer.Ordinal)
            .ToArray();

        string actualJson = SnapshotTestHelper.Serialize(manifest);

        // Assert
        return SnapshotTestHelper.AssertMatchesJsonSnapshotAsync("endpoint-manifest.json", actualJson, TestContext.Current.CancellationToken);
    }

    private static IEnumerable<EndpointManifestEntry> CreateManifestEntries(RouteEndpoint endpoint)
    {
        var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        var tags = endpoint.Metadata.GetOrderedMetadata<ITagsMetadata>()
            .SelectMany(metadata => metadata.Tags)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"];

        foreach (string method in methods.OrderBy(value => value, StringComparer.Ordinal))
        {
            yield return new EndpointManifestEntry
            {
                Method = method,
                Route = NormalizeRoute(endpoint.RoutePattern.RawText),
                DisplayName = endpoint.DisplayName ?? String.Empty,
                Tags = tags,
                AllowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                AuthorizationPolicies = authorizeData
                    .Select(data => data.Policy)
                    .Where(policy => !String.IsNullOrWhiteSpace(policy))
                    .Select(policy => policy!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(policy => policy, StringComparer.Ordinal)
                    .ToArray(),
                AuthorizationRoles = authorizeData
                    .SelectMany(data => SplitCsv(data.Roles))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(role => role, StringComparer.Ordinal)
                    .ToArray(),
                AuthenticationSchemes = authorizeData
                    .SelectMany(data => SplitCsv(data.AuthenticationSchemes))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(scheme => scheme, StringComparer.Ordinal)
                    .ToArray()
            };
        }
    }

    private static IEnumerable<string> SplitCsv(string? value)
    {
        if (String.IsNullOrWhiteSpace(value))
            return [];

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeRoute(string? route)
    {
        if (String.IsNullOrWhiteSpace(route))
            return "/";

        return route.StartsWith('/') ? route : $"/{route}";
    }

    private sealed class EndpointManifestEntry
    {
        public required string Method { get; init; }
        public required string Route { get; init; }
        public required string DisplayName { get; init; }
        public required string[] Tags { get; init; } = [];
        public required bool AllowAnonymous { get; init; }
        public required string[] AuthorizationPolicies { get; init; } = [];
        public required string[] AuthorizationRoles { get; init; } = [];
        public required string[] AuthenticationSchemes { get; init; } = [];
    }
}
