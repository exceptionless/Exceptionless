using Exceptionless.Web.Models;

namespace Exceptionless.Web.Api.Messages;

public record GetRateNotifications(string UserId, string ProjectId, int Page, int Limit, HttpContext Context);
public record CreateRateNotification(string UserId, string ProjectId, NewRateNotificationRule Rule, HttpContext Context);
public record GetRateNotificationById(string UserId, string ProjectId, string RuleId, HttpContext Context);
public record UpdateRateNotification(string UserId, string ProjectId, string RuleId, UpdateRateNotificationRule Rule, HttpContext Context);
public record DeleteRateNotification(string UserId, string ProjectId, string RuleId, HttpContext Context);
public record SnoozeRateNotification(string UserId, string ProjectId, string RuleId, SnoozeRateNotificationRuleRequest Request, HttpContext Context);
public record UnsnoozeRateNotification(string UserId, string ProjectId, string RuleId, HttpContext Context);
