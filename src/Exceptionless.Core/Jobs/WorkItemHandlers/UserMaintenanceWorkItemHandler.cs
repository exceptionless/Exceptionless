using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Foundatio.Utility;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class UserMaintenanceWorkItemHandler : WorkItemHandlerBase {
        private readonly IUserRepository _userRepository;
        private readonly ILockProvider _lockProvider;

        public UserMaintenanceWorkItemHandler(IUserRepository userRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _userRepository = userRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            return _lockProvider.AcquireAsync(nameof(UserMaintenanceWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            const int LIMIT = 100;

            var workItem = context.GetData<UserMaintenanceWorkItem>();
            Log.Info("Received user maintenance work item. Normalize: {0}", workItem.Normalize);

            var results = await _userRepository.GetAllAsync(paging: new PagingOptions().WithLimit(LIMIT)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var user in results.Documents) {
                    if (workItem.Normalize)
                        NormalizeUser(user);
                }

                if (workItem.Normalize)
                    await _userRepository.SaveAsync(results.Documents).AnyContext();

                // Sleep so we are not hammering the backend.
                await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }
        }

        private void NormalizeUser(User user) {
            user.FullName = user.FullName?.Trim();

            string email = user.EmailAddress?.Trim().ToLowerInvariant();
            if (!String.Equals(user.EmailAddress, email)) {
                Log.Info().Message("Normalizing user email address \"{0}\" to \"{1}\"", user.EmailAddress, email).Write();
                user.EmailAddress = email;
            }
        }
    }
}