using System.Net;
using System.Text.Json;
using Exceptionless.Tests.Utility;

namespace Exceptionless.Tests.Extensions;

/// <summary>
/// Test helper for creating RFC 6902 JSON Patch operations arrays.
/// The correct content type for JSON Patch is "application/json-patch+json".
/// Use PatchContent() for FluentClient .Content(string, string) calls.
/// </summary>
public static class JsonPatchHelper
{
    public const string ContentType = "application/json-patch+json";

    /// <summary>
    /// Creates an RFC 6902 JSON Patch operations array with "replace" operations.
    /// </summary>
    public static object[] Patch(params (string path, object? value)[] replacements)
    {
        return replacements.Select(r => (object)new { op = "replace", path = $"/{r.path}", value = r.value }).ToArray();
    }

    /// <summary>
    /// Creates an RFC 6902 JSON Patch operations array with a single "replace" operation.
    /// </summary>
    public static object[] Patch(string path, object? value)
    {
        return [new { op = "replace", path = $"/{path}", value }];
    }

    /// <summary>
    /// Returns a serialized JSON Patch string for use with .Content(string, contentType).
    /// </summary>
    public static string PatchContent(params (string path, object? value)[] replacements)
    {
        return JsonSerializer.Serialize(Patch(replacements));
    }

    /// <summary>
    /// Returns a serialized JSON Patch string with a single replace operation.
    /// </summary>
    public static string PatchContent(string path, object? value)
    {
        return JsonSerializer.Serialize(Patch(path, value));
    }
}

public static class RequestExtensions
{
    public static object[] JsonPatch(params (string path, object? value)[] replacements)
    {
        return JsonPatchHelper.Patch(replacements);
    }

    public static object[] JsonPatch(string path, object? value)
    {
        return JsonPatchHelper.Patch(path, value);
    }

    /// <summary>
    /// Extension to set JSON Patch content with correct content type on AppSendBuilder.
    /// </summary>
    public static AppSendBuilder JsonPatchContent(this AppSendBuilder builder, params (string path, object? value)[] replacements)
    {
        return builder.Content(JsonPatchHelper.PatchContent(replacements), JsonPatchHelper.ContentType);
    }

    /// <summary>
    /// Extension to set JSON Patch content with correct content type on AppSendBuilder.
    /// </summary>
    public static AppSendBuilder JsonPatchContent(this AppSendBuilder builder, string path, object? value)
    {
        return builder.Content(JsonPatchHelper.PatchContent(path, value), JsonPatchHelper.ContentType);
    }

    public static AppSendBuilder StatusCodeShouldBeOk(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.OK);
    }

    public static AppSendBuilder StatusCodeShouldBeAccepted(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.Accepted);
    }

    public static AppSendBuilder StatusCodeShouldBeNotFound(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.NotFound);
    }

    public static AppSendBuilder StatusCodeShouldBePaymentRequired(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.PaymentRequired);
    }

    public static AppSendBuilder StatusCodeShouldBeBadRequest(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.BadRequest);
    }

    public static AppSendBuilder StatusCodeShouldBeUnprocessableEntity(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.UnprocessableEntity);
    }

    public static AppSendBuilder StatusCodeShouldBeCreated(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.Created);
    }

    public static AppSendBuilder StatusCodeShouldBeNoContent(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.NoContent);
    }

    public static AppSendBuilder StatusCodeShouldBeUnauthorized(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.Unauthorized);
    }

    public static AppSendBuilder StatusCodeShouldBeForbidden(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.Forbidden);
    }

    public static AppSendBuilder StatusCodeShouldBeUpgradeRequired(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.UpgradeRequired);
    }

    public static HttpStatusCode? GetExpectedStatus(this HttpRequestMessage requestMessage)
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        requestMessage.Options.TryGetValue(AppSendBuilder.ExpectedStatusKey, out var propertyValue);
        return propertyValue;
    }

    public static void SetExpectedStatus(this HttpRequestMessage requestMessage, HttpStatusCode statusCode)
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        requestMessage.Options.Set(AppSendBuilder.ExpectedStatusKey, statusCode);
    }
}
