﻿namespace Exceptionless.Web.Utility.Results;

public record PermissionResult
{
    public bool Allowed { get; set; }
    public string? Id { get; set; }
    public string? Message { get; set; }

    public int StatusCode { get; set; }

    public static PermissionResult Allow = new() { Allowed = true, StatusCode = StatusCodes.Status200OK };

    public static PermissionResult Deny = new() { Allowed = false, StatusCode = StatusCodes.Status400BadRequest };

    public static PermissionResult DenyWithNotFound(string? id = null)
    {
        return new PermissionResult
        {
            Allowed = false,
            Id = id,
            StatusCode = StatusCodes.Status404NotFound
        };
    }

    public static PermissionResult DenyWithMessage(string message, string? id = null)
    {
        return new PermissionResult
        {
            Allowed = false,
            Id = id,
            Message = message,
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    public static PermissionResult DenyWithStatus(int statusCode, string? message = null, string? id = null)
    {
        return new PermissionResult
        {
            Allowed = false,
            Id = id,
            Message = message,
            StatusCode = statusCode
        };
    }

    public static PermissionResult DenyWithPlanLimitReached(string message, string? id = null)
    {
        return new PermissionResult
        {
            Allowed = false,
            Id = id,
            Message = message,
            StatusCode = StatusCodes.Status426UpgradeRequired
        };
    }


    public static PermissionResult DenyWithPNotImplemented(string message, string? id = null)
    {
        return new PermissionResult
        {
            Allowed = false,
            Id = id,
            Message = message,
            StatusCode = StatusCodes.Status501NotImplemented
        };
    }
}
