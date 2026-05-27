using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Services;
using Exceptionless.Web.Models;
using Foundatio.Queues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers;

[Route(API_PREFIX)]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class StatusController : ExceptionlessApiController
{
    private readonly NotificationService _notificationService;
    private readonly IQueue<EventPost> _eventQueue;
    private readonly IQueue<MailMessage> _mailQueue;
    private readonly IQueue<EventNotification> _notificationQueue;
    private readonly IQueue<WebHookNotification> _webHooksQueue;
    private readonly IQueue<EventUserDescription> _userDescriptionQueue;
    private readonly AppOptions _appOptions;

    public StatusController(
        NotificationService notificationService,
        IQueue<EventPost> eventQueue,
        IQueue<MailMessage> mailQueue,
        IQueue<EventNotification> notificationQueue,
        IQueue<WebHookNotification> webHooksQueue,
        IQueue<EventUserDescription> userDescriptionQueue,
        AppOptions appOptions,
        TimeProvider timeProvider) : base(timeProvider)
    {
        _notificationService = notificationService;
        _eventQueue = eventQueue;
        _mailQueue = mailQueue;
        _notificationQueue = notificationQueue;
        _webHooksQueue = webHooksQueue;
        _userDescriptionQueue = userDescriptionQueue;
        _appOptions = appOptions;
    }

    /// <summary>
    /// Get the info of the API
    /// </summary>
    [AllowAnonymous]
    [HttpGet("about")]
    public IActionResult IndexAsync()
    {
        return Ok(new
        {
            _appOptions.InformationalVersion,
            AppMode = _appOptions.AppMode.ToString(),
            Environment.MachineName
        });
    }

    [HttpGet("queue-stats")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<IActionResult> QueueStatsAsync()
    {
        var eventQueueStats = await _eventQueue.GetQueueStatsAsync();
        var mailQueueStats = await _mailQueue.GetQueueStatsAsync();
        var userDescriptionQueueStats = await _userDescriptionQueue.GetQueueStatsAsync();
        var notificationQueueStats = await _notificationQueue.GetQueueStatsAsync();
        var webHooksQueueStats = await _webHooksQueue.GetQueueStatsAsync();

        return Ok(new
        {
            EventPosts = new
            {
                Active = eventQueueStats.Enqueued,
                eventQueueStats.Deadletter,
                eventQueueStats.Working
            },
            MailMessages = new
            {
                Active = mailQueueStats.Enqueued,
                mailQueueStats.Deadletter,
                mailQueueStats.Working
            },
            UserDescriptions = new
            {
                Active = userDescriptionQueueStats.Enqueued,
                userDescriptionQueueStats.Deadletter,
                userDescriptionQueueStats.Working
            },
            Notifications = new
            {
                Active = notificationQueueStats.Enqueued,
                notificationQueueStats.Deadletter,
                notificationQueueStats.Working
            },
            WebHooks = new
            {
                Active = webHooksQueueStats.Enqueued,
                webHooksQueueStats.Deadletter,
                webHooksQueueStats.Working
            }
        });
    }

    [HttpPost("notifications/release")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<ActionResult<ReleaseNotification>> PostReleaseNotificationAsync(ValueFromBody<string> message, bool critical = false)
    {
        var notification = await _notificationService.SendReleaseNotificationAsync(message.Value, critical);
        return Ok(notification);
    }

    /// <summary>
    /// Returns the current system notification messages.
    /// </summary>
    [HttpGet("notifications/system")]
    public async Task<ActionResult<SystemNotification>> GetSystemNotificationAsync()
    {
        var notification = await _notificationService.GetSystemNotificationAsync();
        if (notification is null)
            return Ok();

        return Ok(notification);
    }

    [HttpPost("notifications/system")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<ActionResult<SystemNotification>> PostSystemNotificationAsync(ValueFromBody<string> message, bool publish = true)
    {
        if (String.IsNullOrWhiteSpace(message?.Value))
            return BadRequest();

        var notification = await _notificationService.SetSystemNotificationAsync(message.Value, publish);
        return Ok(notification);
    }

    [HttpDelete("notifications/system")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<IActionResult> RemoveSystemNotificationAsync(bool publish = true)
    {
        await _notificationService.ClearSystemNotificationAsync(publish);
        return Ok();
    }

    /// <summary>
    /// Returns the current notification settings state for the admin management page.
    /// </summary>
    [HttpGet("notifications/settings")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<ActionResult<NotificationSettingsResponse>> GetNotificationSettingsAsync()
    {
        var notification = await _notificationService.GetSystemNotificationAsync();
        return Ok(new NotificationSettingsResponse
        {
            ConfiguredSystemNotificationMessage = _appOptions.NotificationMessage,
            SystemNotification = notification
        });
    }

    /// <summary>
    /// Force all connected clients to reload their browser.
    /// </summary>
    [HttpPost("notifications/force-refresh")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<ActionResult<ReleaseNotification>> ForceRefreshAsync(ValueFromBody<string?>? message)
    {
        var notification = await _notificationService.SendReleaseNotificationAsync(message?.Value, critical: true);
        return Ok(notification);
    }
}
