using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class UserMaintenanceWorkItemHandler : WorkItemHandlerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IMailer _mailer;
    private readonly ILockProvider _lockProvider;

    public UserMaintenanceWorkItemHandler(IUserRepository userRepository, ICacheClient cacheClient,
        IMessageBus messageBus, IMailer mailer, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _userRepository = userRepository;
        _mailer = mailer;
        _lockProvider = new CacheLockProvider(cacheClient, messageBus);
    }

    public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new())
    {
        return _lockProvider.AcquireAsync(nameof(UserMaintenanceWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        const int LIMIT = 100;

        var workItem = context.GetData<UserMaintenanceWorkItem>();
        Log.LogInformation("Received user maintenance work item. Normalize={Normalize} ResendVerifyEmailAddressEmails={ResendVerifyEmailAddressEmails}", workItem.Normalize, workItem.ResendVerifyEmailAddressEmails);

        var results = await _userRepository.GetAllAsync(o => o.PageLimit(LIMIT));
        while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            if (workItem.Normalize)
                await NormalizeUsersAsync(results.Documents);
            if (workItem.ResendVerifyEmailAddressEmails)
                await ResendVerifyEmailAddressEmailsAsync(results.Documents);

            // Sleep so we are not hammering the backend.
            await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5));

            if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync())
                break;

            if (results.Documents.Count > 0)
                await context.RenewLockAsync();
        }
    }

    private Task NormalizeUsersAsync(IReadOnlyCollection<User> users)
    {
        foreach (var user in users)
        {
            user.FullName = user.FullName.Trim();
            string email = user.EmailAddress.Trim().ToLowerInvariant();
            if (!String.Equals(user.EmailAddress, email))
            {
                Log.LogInformation("Normalizing user email address {EmailAddress} to {NewEmailAddress}", user.EmailAddress, email);
                user.EmailAddress = email;
            }
        }

        return _userRepository.SaveAsync(users);
    }

    private async Task ResendVerifyEmailAddressEmailsAsync(IReadOnlyCollection<User> users)
    {
        var unverifiedUsers = users.Where(u => !u.IsEmailAddressVerified).ToList();
        if (unverifiedUsers.Count is 0)
            return;

        foreach (var user in unverifiedUsers)
            user.MarkEmailAddressUnverified();

        await _userRepository.SaveAsync(unverifiedUsers);

        foreach (var user in unverifiedUsers)
            await _mailer.SendUserEmailVerifyAsync(user);
    }
}
