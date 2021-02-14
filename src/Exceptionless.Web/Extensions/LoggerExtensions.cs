using System;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Exceptionless.Web.Extensions {
    internal static class LoggerExtensions {
        
        private static readonly Action<ILogger, string?, string, Exception?> _projectRouteDoesNotMatch =
            LoggerMessage.Define<string?, string>(
                LogLevel.Information,
                new EventId(0, nameof(ProjectRouteDoesNotMatch)),
                "Project {RequestProjectId} from request doesn't match project route id {RouteProjectId}");
        
        private static readonly Action<ILogger, int, string, Exception?> _removingZapierUrls =
            LoggerMessage.Define<int, string>(
                LogLevel.Information,
                new EventId(1, nameof(RemovingZapierUrls)),
                "Removing {Count} zapier urls matching: {Url}");

        private static readonly Action<ILogger, long, string, Exception?> _removedTokens =
            LoggerMessage.Define<long, string>(
                LogLevel.Information,
                new EventId(2, nameof(RemovedTokens)),
                "Removed {RemovedCount} tokens for user: {UserId}");
        
        private static readonly Action<ILogger, long, Exception?> _submissionTooLarge =
            LoggerMessage.Define<long>(
                LogLevel.Information,
                new EventId(3, nameof(SubmissionTooLarge)),
                "Event submission discarded for being too large: {@value} bytes.");
        
        private static readonly Action<ILogger, string, string, Exception?> _userDeletingProject =
            LoggerMessage.Define<string, string>(
                LogLevel.Information,
                new EventId(4, nameof(UserDeletingProject)),
                "User {User} deleting project: {ProjectName}.");
        
        private static readonly Action<ILogger, string, string, string, Exception?> _userDeletingOrganization =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Information,
                new EventId(5, nameof(UserDeletingOrganization)),
                "User {User} deleting organization: {OrganizationName}  ({OrganizationId})");
         
        private static readonly Action<ILogger, string, Exception?> _userLoggedIn =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(6, nameof(UserLoggedIn)),
                "{EmailAddress} logged in.");
         
        private static readonly Action<ILogger, string, Exception?> _userSignedUp =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(7, nameof(UserSignedUp)),
                "{EmailAddress} signed up.");
         
        private static readonly Action<ILogger, string, Exception?> _userChangedPassword =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(8, nameof(UserChangedPassword)),
                "{EmailAddress} changed their password.");
         
        private static readonly Action<ILogger, string, Exception?> _userForgotPassword =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(9, nameof(UserForgotPassword)),
                "{EmailAddress} forgot their password.");
         
        private static readonly Action<ILogger, string, Exception?> _userResetPassword =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(10, nameof(UserResetPassword)),
                "{EmailAddress} reset their password.");
         
        private static readonly Action<ILogger, string, Exception?> _userCanceledResetPassword =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(11, nameof(UserCanceledResetPassword)),
                "{EmailAddress} canceled the reset password.");
         
        private static readonly Action<ILogger, string, Exception?> _changedUserPassword =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(12, nameof(ChangedUserPassword)),
                "Changed password for {EmailAddress}");
         
        private static readonly Action<ILogger, string, Exception?> _userJoinedFromInvite =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(13, nameof(UserJoinedFromInvite)),
                "{EmailAddress} joined from invite.");
         
        private static readonly Action<ILogger, string, Exception?> _markedInvitedUserAsVerified =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(14, nameof(MarkedInvitedUserAsVerified)),
                "Marking the invited users email address {EmailAddress} as verified.");
         
        private static readonly Action<ILogger, string, string, Exception?> _userRemovedExternalLogin =
            LoggerMessage.Define<string, string>(
                LogLevel.Information,
                new EventId(15, nameof(UserRemovedExternalLogin)),
                "{EmailAddress} removed an external login: {ProviderName}");
         
        private static readonly Action<ILogger, string, string, Exception?> _unableToAddInvitedUserInvalidToken =
            LoggerMessage.Define<string, string>(
                LogLevel.Information,
                new EventId(16, nameof(UnableToAddInvitedUserInvalidToken)),
                "Unable to add the invited user {EmailAddress}. Invalid invite token: {Token}");
         
        private static readonly Action<ILogger, long, string, Exception?> _removedUserTokens =
            LoggerMessage.Define<long, string>(
                LogLevel.Information,
                new EventId(17, nameof(RemovedUserTokens)),
                "Removed user {TokenCount} tokens for {EmailAddress}");
        
        public static void ProjectRouteDoesNotMatch(this ILogger logger, string? requestProjectId, string targetUrl)
            => _projectRouteDoesNotMatch(logger, requestProjectId, targetUrl, null);

        public static void RemovingZapierUrls(this ILogger logger, int count, string targetUrl)
            => _removingZapierUrls(logger, count, targetUrl, null);

        public static void RemovedTokens(this ILogger logger, long removedCount, string userId)
            => _removedTokens(logger, removedCount, userId, null);

        public static void SubmissionTooLarge(this ILogger logger, long size)
            => _submissionTooLarge(logger, size, null);

        public static void UserDeletingProject(this ILogger logger, string user, string projectName)
            => _userDeletingProject(logger, user, projectName, null);

        public static void UserDeletingOrganization(this ILogger logger, string user, string organizationName, string organizationId)
            => _userDeletingOrganization(logger, user, organizationName, organizationId, null);

        public static void UserLoggedIn(this ILogger logger, string email)
            => _userLoggedIn(logger, email, null);
        
        public static void UserSignedUp(this ILogger logger, string email)
            => _userSignedUp(logger, email, null);
        
        public static void UserChangedPassword(this ILogger logger, string email)
            => _userChangedPassword(logger, email, null);
        
        public static void UserForgotPassword(this ILogger logger, string email)
            => _userForgotPassword(logger, email, null);
        
        public static void UserResetPassword(this ILogger logger, string email)
            => _userResetPassword(logger, email, null);
        
        public static void UserCanceledResetPassword(this ILogger logger, string email)
            => _userCanceledResetPassword(logger, email, null);
        
        public static void ChangedUserPassword(this ILogger logger, string email)
            => _changedUserPassword(logger, email, null);
        
        public static void UserJoinedFromInvite(this ILogger logger, string email)
            => _userJoinedFromInvite(logger, email, null);
        
        public static void MarkedInvitedUserAsVerified(this ILogger logger, string email)
            => _markedInvitedUserAsVerified(logger, email, null);
        
        public static void UserRemovedExternalLogin(this ILogger logger, string email, string providerName)
            => _userRemovedExternalLogin(logger, email, providerName, null);
        
        public static void UnableToAddInvitedUserInvalidToken(this ILogger logger, string email, string token)
            => _unableToAddInvitedUserInvalidToken(logger, email, token, null);
        
        public static void RemovedUserTokens(this ILogger logger, long total, string email)
            => _removedUserTokens(logger, total, email, null);
    }
}