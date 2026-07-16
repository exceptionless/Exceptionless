using Exceptionless.Web.Models.Admin;

namespace Exceptionless.Web.Api.Messages;

public record GetOAuthApplications;
public record CreateOAuthApplicationMessage(NewOAuthApplication Model, HttpContext Context);
public record UpdateOAuthApplicationMessage(string Id, UpdateOAuthApplication Model, HttpContext Context);
public record DeleteOAuthApplicationMessage(string Id);
