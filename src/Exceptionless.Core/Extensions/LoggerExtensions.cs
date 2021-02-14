using System;
using System.Net;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Exceptionless.Core.Extensions {
    internal static class LoggerExtensions {

        private static readonly Action<ILogger, string, string, string, Exception?> _recordWebHook =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Trace,
                new EventId(0, nameof(RecordWebHook)),
                "Process web hook call: id={Id} project={project} url={Url}");

        private static readonly Action<ILogger, Exception?> _webHookCancelled =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(1, nameof(WebHookCancelled)),
                "Web hook cancelled: Web hook is disabled");

        private static readonly Action<ILogger, long, DateTime, Exception?> _webHookCancelledBackoff =
            LoggerMessage.Define<long, DateTime>(
                LogLevel.Information,
                new EventId(2, nameof(WebHookCancelledBackoff)),
                "Web hook cancelled due to {FailureCount} consecutive failed attempts. Will be allowed to try again at {NextAttempt}.");

        private static readonly Action<ILogger, HttpStatusCode?, string, string, string, Exception?> _webHookTimeout =
            LoggerMessage.Define<HttpStatusCode?, string, string, string>(
                LogLevel.Error,
                new EventId(3, nameof(WebHookTimeout)),
                "Timeout calling web hook: status={Status} org={organization} project={project} url={Url}");

        private static readonly Action<ILogger, HttpStatusCode?, string, string, string, Exception?> _webHookError =
            LoggerMessage.Define<HttpStatusCode?, string, string, string>(
                LogLevel.Error,
                new EventId(4, nameof(WebHookError)),
                "Error calling web hook: status={Status} org={organization} project={project} url={Url}");

        private static readonly Action<ILogger, HttpStatusCode?, string, string, string, Exception?> _webHookComplete =
            LoggerMessage.Define<HttpStatusCode?, string, string, string>(
                LogLevel.Information,
                new EventId(5, nameof(WebHookError)),
                "Web hook POST complete: status={Status} org={organization} project={project} url={Url}");

        private static readonly Action<ILogger, string, HttpStatusCode?, string, string, string, Exception?> _webHookDisabledStatusCode =
            LoggerMessage.Define<string, HttpStatusCode?, string, string, string>(
                LogLevel.Warning,
                new EventId(6, nameof(WebHookDisabledStatusCode)),
                "Disabling Web hook instance {WebHookId} due to status code: status={Status} org={organization} project={project} url={Url}");

        private static readonly Action<ILogger, string, Exception?> _webHookDisabledTooManyErrors =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(7, nameof(WebHookDisabledTooManyErrors)),
                "Disabling Web hook instance {WebHookId} due to too many consecutive failures.");

        private static readonly Action<ILogger, Exception?> _cleanupFinished =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(8, nameof(CleanupFinished)),
                "Finished cleaning up data");

        private static readonly Action<ILogger, long, Exception?> _cleanupOrganizationSoftDeletes =
            LoggerMessage.Define<long>(
                LogLevel.Information,
                new EventId(9, nameof(CleanupOrganizationSoftDeletes)),
                "Cleaning up {OrganizationTotal} soft deleted organization(s)");

        private static readonly Action<ILogger, long, Exception?> _cleanupProjectSoftDeletes =
            LoggerMessage.Define<long>(
                LogLevel.Information,
                new EventId(10, nameof(CleanupProjectSoftDeletes)),
                "Cleaning up {ProjectTotal} soft deleted project(s)");

        private static readonly Action<ILogger, long, Exception?> _cleanupStackSoftDeletes =
            LoggerMessage.Define<long>(
                LogLevel.Information,
                new EventId(11, nameof(CleanupStackSoftDeletes)),
                "Cleaning up {StackTotal} soft deleted stack(s)");

        private static readonly Action<ILogger, string, string, Exception?> _removeOrganizationStart =
            LoggerMessage.Define<string, string>(
                LogLevel.Information,
                new EventId(12, nameof(RemoveOrganizationStart)),
                "Removing organization: {Organization} ({OrganizationId})");

        private static readonly Action<ILogger, string, string, long, long, long, Exception?> _removeOrganizationComplete =
            LoggerMessage.Define<string, string, long, long, long>(
                LogLevel.Information,
                new EventId(14, nameof(RemoveOrganizationComplete)),
                "Removed organization: {Organization} ({OrganizationId}), Removed {RemovedProjects} Projects, {RemovedStacks} Stacks, {RemovedEvents} Events");

        private static readonly Action<ILogger, string, string, Exception?> _removeProjectStart =
            LoggerMessage.Define<string, string>(
                LogLevel.Information,
                new EventId(15, nameof(RemoveProjectStart)),
                "Removing project: {Project} ({ProjectId})");

        private static readonly Action<ILogger, string, string, long, long, Exception?> _removeProjectComplete =
            LoggerMessage.Define<string, string, long, long>(
                LogLevel.Information,
                new EventId(16, nameof(RemoveProjectComplete)),
                "Removed project: {Project} ({ProjectId}), Removed {RemovedStacks} Stacks, {RemovedEvents} Events");

        private static readonly Action<ILogger, string, Exception?> _removeStackStart =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(17, nameof(RemoveStackStart)),
                "Removing stack: {StackId}");

        private static readonly Action<ILogger, string, long, Exception?> _removeStackComplete =
            LoggerMessage.Define<string, long>(
                LogLevel.Information,
                new EventId(18, nameof(RemoveStackComplete)),
                "Removed stack: {StackId}, Removed {RemovedEvents} Events");

        private static readonly Action<ILogger, DateTime, string, string, Exception?> _retentionEnforcementStart =
            LoggerMessage.Define<DateTime, string, string>(
                LogLevel.Information,
                new EventId(19, nameof(RetentionEnforcementStart)),
                "Enforcing event count limits older than {RetentionPeriod:g} for organization {OrganizationName} ({OrganizationId}).");

        private static readonly Action<ILogger, string, string, long, Exception?> _retentionEnforcementComplete =
            LoggerMessage.Define<string, string, long>(
                LogLevel.Information,
                new EventId(20, nameof(RetentionEnforcementComplete)),
                "Enforced retention period for {OrganizationName} ({OrganizationId}), Removed {RemovedEvents} Events");

        public static void RetentionEnforcementComplete(this ILogger logger, string organizationName, string organizationId, long removedEvents)
            => _retentionEnforcementComplete(logger, organizationName, organizationId, removedEvents, null);

        public static void RetentionEnforcementStart(this ILogger logger, DateTime cutoff, string organizationName, string organizationId)
            => _retentionEnforcementStart(logger, cutoff, organizationName, organizationId, null);

        public static void RemoveStackComplete(this ILogger logger, string stackId, long removedEvents)
            => _removeStackComplete(logger, stackId, removedEvents, null);

        public static void RemoveStackStart(this ILogger logger, string stackId)
            => _removeStackStart(logger, stackId, null);

        public static void RemoveProjectComplete(this ILogger logger, string projectName, string projectId, long removedStacks, long removedEvents)
            => _removeProjectComplete(logger, projectName, projectId, removedStacks, removedEvents, null);

        public static void RemoveProjectStart(this ILogger logger, string projectName, string projectId)
            => _removeProjectStart(logger, projectName, projectId, null);

        public static void RemoveOrganizationComplete(this ILogger logger, string organizationName, string organizationId, long removedProjects, long removedStacks, long removedEvents)
            => _removeOrganizationComplete(logger, organizationName, organizationId, removedProjects, removedStacks, removedEvents, null);

        public static void RemoveOrganizationStart(this ILogger logger, string organizationName, string organizationId)
            => _removeOrganizationStart(logger, organizationName, organizationId, null);

        public static void CleanupStackSoftDeletes(this ILogger logger, long total)
            => _cleanupStackSoftDeletes(logger, total, null);

        public static void CleanupProjectSoftDeletes(this ILogger logger, long total)
            => _cleanupProjectSoftDeletes(logger, total, null);

        public static void CleanupOrganizationSoftDeletes(this ILogger logger, long total)
            => _cleanupOrganizationSoftDeletes(logger, total, null);

        public static void CleanupFinished(this ILogger logger)
            => _cleanupFinished(logger, null);

        public static void WebHookDisabledTooManyErrors(this ILogger logger, string webHookId)
            => _webHookDisabledTooManyErrors(logger, webHookId, null);

        public static void WebHookDisabledStatusCode(this ILogger logger, string webHookId, HttpStatusCode? statusCode, string organizationId, string projectId, string url)
            => _webHookDisabledStatusCode(logger, webHookId, statusCode, organizationId, projectId, url, null);

        public static void WebHookComplete(this ILogger logger, HttpStatusCode? statusCode, string organizationId, string projectId, string url)
            => _webHookComplete(logger, statusCode, organizationId, projectId, url, null);

        public static void WebHookError(this ILogger logger, HttpStatusCode? statusCode, string organizationId, string projectId, string url, Exception exception)
            => _webHookError(logger, statusCode, organizationId, projectId, url, exception);

        public static void WebHookTimeout(this ILogger logger, HttpStatusCode? statusCode, string organizationId, string projectId, string url, Exception exception)
            => _webHookTimeout(logger, statusCode, organizationId, projectId, url, exception);

        public static void WebHookCancelledBackoff(this ILogger logger, long consecutiveErrors, DateTime nextAttemptAllowedAt)
            => _webHookCancelledBackoff(logger, consecutiveErrors, nextAttemptAllowedAt, null);

        public static void WebHookCancelled(this ILogger logger)
            => _webHookCancelled(logger, null);

        public static void RecordWebHook(this ILogger logger, string id, string projectId, string url)
            => _recordWebHook(logger, id, projectId, url, null);
    }
}
