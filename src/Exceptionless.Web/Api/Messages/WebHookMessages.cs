using System.Text.Json;
using Exceptionless.Web.Models;

namespace Exceptionless.Web.Api.Messages;

public record GetWebHooksByProject(string ProjectId, int Page, int Limit);
public record GetWebHookById(string Id);
public record CreateWebHook(NewWebHook WebHook);
public record DeleteWebHooks(string[] Ids);
public record SubscribeWebHook(JsonDocument Data, int ApiVersion);
public record UnsubscribeWebHook(JsonDocument Data);
public record TestWebHook;
