﻿using System.Net;
using Exceptionless.Tests.Utility;

namespace Exceptionless.Tests.Extensions;

public static class RequestExtensions
{
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

    public static AppSendBuilder StatusCodeShouldBeCreated(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.Created);
    }

    public static AppSendBuilder StatusCodeShouldBeUnauthorized(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.Unauthorized);
    }

    public static AppSendBuilder StatusCodeShouldBeUpgradeRequired(this AppSendBuilder builder)
    {
        return builder.ExpectedStatus(HttpStatusCode.UpgradeRequired);
    }

    public static HttpStatusCode? GetExpectedStatus(this HttpRequestMessage requestMessage)
    {
        if (requestMessage is null)
            throw new ArgumentNullException(nameof(requestMessage));

        requestMessage.Options.TryGetValue(AppSendBuilder.ExpectedStatusKey, out var propertyValue);
        return propertyValue;
    }

    public static void SetExpectedStatus(this HttpRequestMessage requestMessage, HttpStatusCode statusCode)
    {
        if (requestMessage is null)
            throw new ArgumentNullException(nameof(requestMessage));

        requestMessage.Options.Set(AppSendBuilder.ExpectedStatusKey, statusCode);
    }
}
