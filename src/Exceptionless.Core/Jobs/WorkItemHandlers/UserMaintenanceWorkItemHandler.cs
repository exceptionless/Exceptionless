using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class UserMaintenanceWorkItemHandler : WorkItemHandlerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILockProvider _lockProvider;

    public UserMaintenanceWorkItemHandler(IUserRepository userRepository, ICacheClient cacheClient,
        IMessageBus messageBus, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _userRepository = userRepository;
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
        Log.LogInformation("Received user maintenance work item. Normalize={Normalize} ResetVerifyEmailAddressToken={ResendVerifyEmailAddressEmails}", workItem.Normalize, workItem.ResetVerifyEmailAddressToken);

        var results = await _userRepository.GetAllAsync(o => o.PageLimit(LIMIT));
        while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            if (workItem.Normalize)
            {
                await NormalizeUsersAsync(results.Documents);
                continue;
            }

            if (workItem.ResetVerifyEmailAddressToken)
            {
                await ResetVerifyEmailAddressTokenAndExpirationAsync(results.Documents);
                continue;
            }

            // Sleep so we are not hammering the backend.
            await Task.Delay(TimeSpan.FromSeconds(2.5));

            if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync())
                break;

            if (results.Documents.Count > 0)
                await context.RenewLockAsync();
        }
    }

    private async Task NormalizeUsersAsync(IReadOnlyCollection<User> users)
    {
        var usersToSave = new List<User>(users.Count);
        foreach (var user in users)
        {
            string fullName = user.FullName.Trim();
            string email = user.EmailAddress.Trim().ToLowerInvariant();
            if (String.Equals(user.FullName, fullName) && String.Equals(user.EmailAddress, email))
                continue;

            Log.LogInformation("Normalizing user email address {EmailAddress} to {NewEmailAddress}", user.EmailAddress, email);
            user.FullName = fullName;
            user.EmailAddress = email;
            usersToSave.Add(user);
        }

        if (usersToSave.Count > 0)
            await _userRepository.SaveAsync(usersToSave);
    }

    private async Task ResetVerifyEmailAddressTokenAndExpirationAsync(IEnumerable<User> users)
    {
        var unverifiedUsers = users.Where(u => !u.IsEmailAddressVerified).ToList();
        if (unverifiedUsers.Count is 0)
            return;

        foreach (var user in unverifiedUsers)
        {
            user.ResetVerifyEmailAddressTokenAndExpiration();
            Log.LogInformation("Reset verify email address token and expiration for {EmailAddress}", user.EmailAddress);
        }

        await _userRepository.SaveAsync(unverifiedUsers);
    }
}
