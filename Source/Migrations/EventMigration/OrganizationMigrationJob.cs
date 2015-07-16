using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Utility;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using MongoDB.Driver;
using Nest;
using NLog.Fluent;
using Token = Exceptionless.Core.Models.Token;

#pragma warning disable 1998

namespace Exceptionless.EventMigration {
    public class OrganizationMigrationJob : JobBase {
        private static readonly DateTime MIN_OBJECTID_DATE = new DateTime(2000, 1, 1);
        private readonly IValidator<Organization> _organizationValidator;
        private readonly OrganizationRepository _organizationRepository;
        private readonly OrganizationMigrationRepository _organizationMigrationRepository;
        private readonly IValidator<Project> _projectValidator;
        private readonly ProjectRepository _projectRepository;
        private readonly ProjectMigrationRepository _projectMigrationRepository;
        private readonly IValidator<Token> _tokenValidator;
        private readonly TokenRepository _tokenRepository;
        private readonly TokenMigrationRepository _tokenMigrationRepository;
        private readonly IValidator<User> _userValidator;
        private readonly UserRepository _userRepository;
        private readonly UserMigrationRepository _userMigrationRepository;
        private readonly IValidator<WebHook> _webHookValidator;
        private readonly WebHookRepository _webHookRepository;
        private readonly WebHookMigrationRepository _webHookMigrationRepository;
        private readonly ILockProvider _lockProvider;
        private readonly ICacheClient _cache;

        private readonly int _batchSize;
        private static IPAddress _ipAddress;

        public OrganizationMigrationJob(
            IElasticClient client,
            ICacheClient cacheClient,
            OrganizationIndex organizationIndex,
            IValidator<Organization> organizationValidator,
            IValidator<Project> projectValidator,
            IValidator<Token> tokenValidator,
            IValidator<User> userValidator,
            IValidator<WebHook> webHookValidator,
            ILockProvider lockProvider, 
            ICacheClient cache) {
            var mongoDatabase = GetMongoDatabase();
            _organizationValidator = organizationValidator;
            _organizationRepository = new OrganizationRepository(client, organizationIndex, organizationValidator, cacheClient);
            _organizationMigrationRepository = new OrganizationMigrationRepository(mongoDatabase, organizationValidator);
            _projectValidator = projectValidator;
            _projectRepository = new ProjectRepository(client, organizationIndex, projectValidator, cacheClient);
            _projectMigrationRepository = new ProjectMigrationRepository(mongoDatabase, projectValidator);
            _tokenValidator = tokenValidator;
            _tokenRepository = new TokenRepository(client, organizationIndex, tokenValidator, cacheClient);
            _tokenMigrationRepository = new TokenMigrationRepository(mongoDatabase, tokenValidator);
            _userValidator = userValidator;
            _userRepository = new UserRepository(client, organizationIndex, userValidator, cacheClient);
            _userMigrationRepository = new UserMigrationRepository(mongoDatabase, userValidator);
            _webHookValidator = webHookValidator;
            _webHookRepository = new WebHookRepository(client, organizationIndex, webHookValidator, cacheClient);
            _webHookMigrationRepository = new WebHookMigrationRepository(mongoDatabase, webHookValidator);
            _lockProvider = lockProvider;
            _cache = cache;

            _batchSize = MigrationSettings.Current.MigrationBatchSize;
        }

        protected override IDisposable GetJobLock() {
            return _lockProvider.AcquireLock("OrganizationMigrationJob");
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            OutputPublicIp();

            var result = MigrateOrganizations();
            if (result != JobResult.Success)
                return result;

            result = MigrateProjects();
            if (result != JobResult.Success)
                return result;

            result = MigrateTokens();
            if (result != JobResult.Success)
                return result;

            result = MigrateUsers();
            if (result != JobResult.Success)
                return result;

            result = MigrateWebHooks();

            _cache.FlushAll();
            Log.Info().Message("Clearing the cache").Write();

            return result;
        }

        private JobResult MigrateOrganizations() {
            int total = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var mostRecent = _cache.Get<string>("migration-organizationid");
            var items = _organizationMigrationRepository.GetOldest(mostRecent, _batchSize);
            while (items.Count > 0) {
                items.ForEach(organization => {
                    var validationResult = _organizationValidator.Validate(organization);
                    Debug.Assert(validationResult.IsValid, validationResult.Errors.ToErrorMessage());
                    SetCreatedAndModifiedDates(organization);
 
                    UpdateUsage(organization.Usage);
                    UpdateUsage(organization.OverageHours);
                });

                var organizationsWithUsers = items.Where(o => _userMigrationRepository.GetByOrganizationId(o.Id).Count > 0).ToList();
                Debug.Assert(organizationsWithUsers.Count == items.Count, "One or more organizations do not have any users");

                Log.Info().Message("Migrating organizations {0:N0} total {1:N0}/s...", total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();
                try {
                    _organizationRepository.Add(organizationsWithUsers, sendNotification: false);
                } catch (Exception ex) {
                    Debugger.Break();
                    Log.Error().Exception(ex).Message("An error occurred while migrating organizations").Write();
                    return JobResult.FromException(ex, String.Format("An error occurred while migrating organizations: {0}", ex.Message));
                }

                var lastId = items.Last().Id;
                _cache.Set("migration-organizationid", lastId);
                items = _organizationMigrationRepository.GetOldest(lastId, _batchSize);
                total += items.Count;
            }

            Log.Info().Message("Finished migrating organizations.").Write();
            return JobResult.Success;
        }

        private void UpdateUsage(ICollection<UsageInfo> usage) {
            var usagesToRemove = new List<UsageInfo>();

            foreach (var ui in usage) {
                if (ui.Limit == 0 && ui.TooBig > 0) {
                    ui.Limit = ui.TooBig;
                    ui.TooBig = 0;
                } else if (ui.TooBig > 0 && ui.TooBig % 1000 == 0) {
                    ui.TooBig = 0;
                }

                if (ui.Limit <= 0)
                    usagesToRemove.Add(ui);
            }

            usagesToRemove.ForEach(u => usage.Remove(u));
        }

        private JobResult MigrateProjects() {
            int total = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var mostRecent = _cache.Get<string>("migration-projectid");
            var items = _projectMigrationRepository.GetOldest(mostRecent, _batchSize);
            while (items.Count > 0) {
                items.ForEach(project => {
                    var validationResult = _projectValidator.Validate(project);
                    Debug.Assert(validationResult.IsValid, validationResult.Errors.ToErrorMessage());
                    SetCreatedAndModifiedDates(project);

                    var settingsToRemove = new List<string>();
                    project.NotificationSettings.ForEach(pair => {
                        if (_userMigrationRepository.GetById(pair.Key, true) == null)
                            settingsToRemove.Add(pair.Key);
                    });

                    settingsToRemove.ForEach(s => project.NotificationSettings.Remove(s));
                });
              
                var projectsWithOrganization = items.Where(p => _organizationRepository.GetById(p.OrganizationId, true) != null).ToList();
                Debug.Assert(projectsWithOrganization.Count == items.Count, "One or more projects do not have any organizations");

                Log.Info().Message("Migrating projects {0:N0} total {1:N0}/s...", total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();
                try {
                    _projectRepository.Add(projectsWithOrganization, sendNotification: false);
                } catch (Exception ex) {
                    Debugger.Break();
                    Log.Error().Exception(ex).Message("An error occurred while migrating projects").Write();
                    return JobResult.FromException(ex, String.Format("An error occurred while migrating projects: {0}", ex.Message));
                }

                var lastId = items.Last().Id;
                _cache.Set("migration-projectid", lastId);
                items = _projectMigrationRepository.GetOldest(lastId, _batchSize);
                total += items.Count;
            }

            Log.Info().Message("Finished migrating projects.").Write();
            return JobResult.Success;
        }
        
        private JobResult MigrateTokens() {
            int total = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var mostRecent = _cache.Get<string>("migration-tokenid");
            var items = _tokenMigrationRepository.GetOldest(mostRecent, _batchSize);
            while (items.Count > 0) {
                items.ForEach(token => {
                    var validationResult = _tokenValidator.Validate(token);
                    Debug.Assert(validationResult.IsValid, validationResult.Errors.ToErrorMessage());
                });

                Log.Info().Message("Migrating tokens {0:N0} total {1:N0}/s...", total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();
                try {
                    _tokenRepository.Add(items, sendNotification: false);
                } catch (Exception ex) {
                    Debugger.Break();
                    Log.Error().Exception(ex).Message("An error occurred while migrating tokens").Write();
                    return JobResult.FromException(ex, String.Format("An error occurred while migrating tokens: {0}", ex.Message));
                }

                var lastId = items.Last().Id;
                _cache.Set("migration-tokenid", lastId);
                items = _tokenMigrationRepository.GetOldest(lastId, _batchSize);
                total += items.Count;
            }

            Log.Info().Message("Finished migrating tokens.").Write();
            return JobResult.Success;
        }
        
        private JobResult MigrateUsers() {
            int total = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var mostRecent = _cache.Get<string>("migration-userid");
            var items = _userMigrationRepository.GetOldest(mostRecent, _batchSize);
            while (items.Count > 0) {
                items.ForEach(user => {
                    if (!user.IsEmailAddressVerified && String.IsNullOrEmpty(user.VerifyEmailAddressToken)) {
                        user.CreateVerifyEmailAddressToken();
                        // TODO: Do we want to resend the verify email address token?
                    }

                    var validationResult = _userValidator.Validate(user);
                    Debug.Assert(validationResult.IsValid, validationResult.Errors.ToErrorMessage());
                    SetCreatedAndModifiedDates(user);
                });

                Log.Info().Message("Migrating users {0:N0} total {1:N0}/s...", total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();
                try {
                    _userRepository.Add(items, sendNotification: false);
                } catch (Exception ex) {
                    Debugger.Break();
                    Log.Error().Exception(ex).Message("An error occurred while migrating users").Write();
                    return JobResult.FromException(ex, String.Format("An error occurred while migrating users: {0}", ex.Message));
                }

                var lastId = items.Last().Id;
                _cache.Set("migration-userid", lastId);
                items = _userMigrationRepository.GetOldest(lastId, _batchSize);
                total += items.Count;
            }

            Log.Info().Message("Finished migrating users.").Write();
            return JobResult.Success;
        }
        
        private JobResult MigrateWebHooks() {
            int total = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var mostRecent = _cache.Get<string>("migration-webHookid");
            var items = _webHookMigrationRepository.GetOldest(mostRecent, _batchSize);
            while (items.Count > 0) {
                items.ForEach(webHook => {
                    var validationResult = _webHookValidator.Validate(webHook);
                    Debug.Assert(validationResult.IsValid, validationResult.Errors.ToErrorMessage());
                    SetCreatedAndModifiedDates(webHook);
                });

                Log.Info().Message("Migrating webHooks {0:N0} total {1:N0}/s...", total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();
                try {
                    _webHookRepository.Add(items, sendNotification: false);
                } catch (Exception ex) {
                    Debugger.Break();
                    Log.Error().Exception(ex).Message("An error occurred while migrating webHooks").Write();
                    return JobResult.FromException(ex, String.Format("An error occurred while migrating webHooks: {0}", ex.Message));
                }

                var lastId = items.Last().Id;
                _cache.Set("migration-webHookid", lastId);
                items = _webHookMigrationRepository.GetOldest(lastId, _batchSize);
                total += items.Count;
            }

            Log.Info().Message("Finished migrating webHooks.").Write();
            return JobResult.Success;
        }
        
        private static void SetCreatedAndModifiedDates<T>(T value) where T : class, IIdentity, IHaveCreatedDate {
            ObjectId objectId;
            if (ObjectId.TryParse(value.Id, out objectId) && objectId.CreationTime > MIN_OBJECTID_DATE) {
                if (value.CreatedUtc == DateTime.MinValue)
                    value.CreatedUtc = ObjectId.Parse(value.Id).CreationTime;

            }

            var utcNow = DateTime.UtcNow;
            var dates = value as IHaveDates;
            if (value.CreatedUtc < MIN_OBJECTID_DATE) {
                value.CreatedUtc = utcNow;
                if (dates != null)
                    dates.ModifiedUtc = utcNow;
            }

            if (dates != null && dates.ModifiedUtc == DateTime.MinValue)
                dates.ModifiedUtc = value.CreatedUtc;
        }

        private MongoDatabase GetMongoDatabase() {
            var connectionString = MigrationSettings.Current.MigrationMongoConnectionString;
            if (String.IsNullOrEmpty(connectionString))
                throw new ConfigurationErrorsException("Migration:MongoConnectionString was not found in the app.config.");

            MongoDefaults.MaxConnectionIdleTime = TimeSpan.FromMinutes(1);
            var url = new MongoUrl(connectionString);

            MongoServer server = new MongoClient(url).GetServer();
            return server.GetDatabase(url.DatabaseName);
        }

        private static bool _publicIpDisplayed;

        private static void OutputPublicIp() {
            if (_ipAddress == null)
                _ipAddress = Util.GetExternalIP();

            if (_ipAddress != null && !_publicIpDisplayed) {
                _publicIpDisplayed = true;
                Log.Info().Message("Public IP: " + _ipAddress).Write();
            }
        }
    }

    public class OrganizationMigrationRepository : Repositories.OrganizationRepository {
        public OrganizationMigrationRepository(MongoDatabase database, IValidator<Organization> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(database, validator, cacheClient, messagePublisher) {
            EnableCache = false;
        }
    }

    public class ProjectMigrationRepository : Repositories.ProjectRepository {
        public ProjectMigrationRepository(MongoDatabase database, IValidator<Project> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(database, validator, cacheClient, messagePublisher) {
            EnableCache = false;
        }
    }
    
    public class TokenMigrationRepository : Repositories.TokenRepository {
        public TokenMigrationRepository(MongoDatabase database, IValidator<Token> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(database, validator, cacheClient, messagePublisher) {
            EnableCache = false;
        }
    }
    
    public class UserMigrationRepository : Repositories.UserRepository {
        public UserMigrationRepository(MongoDatabase database, IValidator<User> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(database, validator, cacheClient, messagePublisher) {
            EnableCache = false;
        }
    }
    
    public class WebHookMigrationRepository : Repositories.WebHookRepository {
        public WebHookMigrationRepository(MongoDatabase database, IValidator<WebHook> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(database, validator, cacheClient, messagePublisher) {
            EnableCache = false;
        }
    }
}