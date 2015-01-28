using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Lock;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using OldModels = Exceptionless.EventMigration.Models;

namespace Exceptionless.EventMigration {
    public class EventMigrationJob : JobBase {
        private readonly IElasticClient _elasticClient;
        private readonly EventUpgraderPluginManager _eventUpgraderPluginManager;
        private readonly MongoDatabase _mongoDatabase;
        private readonly StackMigrationRepository _stackRepository;
        private readonly EventMigrationRepository _eventRepository;
        private readonly IGeoIPResolver _geoIpResolver;
        private readonly ILockProvider _lockProvider;

        private readonly int _batchSize;
        private readonly bool _deleteExistingIndexes;
        private readonly bool _resume;
        private readonly bool _skipStacks;
        private readonly bool _skipErrors;

        public EventMigrationJob(IElasticClient elasticClient, EventUpgraderPluginManager eventUpgraderPluginManager, IValidator<Stack> stackValidator, IValidator<PersistentEvent> eventValidator, IGeoIPResolver geoIpResolver, ILockProvider lockProvider) {
            _elasticClient = elasticClient;
            _eventUpgraderPluginManager = eventUpgraderPluginManager;
            _mongoDatabase = GetMongoDatabase();
            _eventRepository = new EventMigrationRepository(elasticClient, eventValidator);
            _stackRepository = new StackMigrationRepository(elasticClient, _eventRepository, stackValidator);
            _geoIpResolver = geoIpResolver;
            _lockProvider = lockProvider;

            _batchSize = ConfigurationManager.AppSettings.GetInt("Migration:BatchSize", 50);
            _deleteExistingIndexes = ConfigurationManager.AppSettings.GetBool("Migration:DeleteExistingIndexes", false);
            _resume = ConfigurationManager.AppSettings.GetBool("Migration:Resume", true);
            _skipStacks = ConfigurationManager.AppSettings.GetBool("Migration:SkipStacks", false);
            _skipErrors = ConfigurationManager.AppSettings.GetBool("Migration:SkipErrors", false);
        }

        private MongoDatabase GetMongoDatabase() {
            var connectionString = ConfigurationManager.ConnectionStrings["Migration:MongoConnectionString"];
            if (connectionString == null)
                throw new ConfigurationErrorsException("Migration:MongoConnectionString was not found in the app.config.");

            if (String.IsNullOrEmpty(connectionString.ConnectionString))
                throw new ConfigurationErrorsException("Migration:MongoConnectionString was not found in the app.config.");

            MongoDefaults.MaxConnectionIdleTime = TimeSpan.FromMinutes(1);
            var url = new MongoUrl(connectionString.ConnectionString);

            MongoServer server = new MongoClient(url).GetServer();
            return server.GetDatabase(url.DatabaseName);
        }

        protected override IDisposable GetJobLock() {
            return _lockProvider.AcquireLock("MigrationJob", TimeSpan.Zero);
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            var publicIp = Util.GetExternalIP();
            if (publicIp != null)
                Log.Info().Message("Public IP: " + publicIp).Write();

            if (_deleteExistingIndexes)
                _elasticClient.DeleteIndex(i => i.AllIndices());

            int total = 0;
            var stopwatch = new Stopwatch();
            if (!_skipStacks) {
                stopwatch.Start();
                var errorStackCollection = GetErrorStackCollection();

                var mostRecentStack = _resume ? _stackRepository.GetMostRecent() : null;
                var query = mostRecentStack != null ? Query.GT(ErrorStackFieldNames.Id, ObjectId.Parse(mostRecentStack.Id)) : Query.Null;
                var stacks = errorStackCollection.Find(query).SetSortOrder(SortBy.Ascending(ErrorStackFieldNames.Id)).SetLimit(_batchSize).ToList();
                while (stacks.Count > 0) {
                    stacks.ForEach(s => {
                        s.Type = s.SignatureInfo != null && s.SignatureInfo.ContainsKey("HttpMethod") && s.SignatureInfo.ContainsKey("Path") ? "404" : "error";

                        if (s.Tags != null)
                            s.Tags.RemoveWhere(t => String.IsNullOrEmpty(t) || t.Length > 255);

                        if (s.Title != null && s.Title.Length > 1000)
                            s.Title = s.Title.Truncate(1000);
                    });

                    Log.Info().Message("Migrating stacks {0:N0} total {1:N0}/s...", total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();
                    try {
                        // TODO: Comment out sendNotifications:false. When I was importing the stacks. I was getting an error where RunPerioid was erroring out due to a null message.
                        _stackRepository.Add(stacks, sendNotification: false);
                    } catch (Exception ex) {
                        Debugger.Break();
                        Log.Error().Exception(ex).Message("An error occurred while migrating stacks").Write();
                        return JobResult.FromException(ex, String.Format("An error occurred while migrating stacks: {0}", ex.Message));
                    }

                    var lastId = stacks.Last().Id;
                    stacks = errorStackCollection.Find(Query.GT(ErrorStackFieldNames.Id, ObjectId.Parse(lastId))).SetSortOrder(SortBy.Ascending(ErrorStackFieldNames.Id)).SetLimit(_batchSize).ToList();
                    total += stacks.Count;
                }
            }

            total = 0;
            stopwatch.Reset();
            if (!_skipErrors) {
                stopwatch.Start();
                var errorCollection = GetErrorCollection();
                var knownStackIds = new List<string>();

                var serializerSettings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore };
                serializerSettings.AddModelConverters();

                var mostRecentEvent = _resume ? _eventRepository.GetMostRecent() : null;
                var query = mostRecentEvent != null ? Query.GT(ErrorFieldNames.Id, ObjectId.Parse(mostRecentEvent.Id)) : Query.Null;
                var errors = errorCollection.Find(query).SetSortOrder(SortBy.Ascending(ErrorFieldNames.Id)).SetLimit(_batchSize).ToList();
                while (errors.Count > 0) {
                    Log.Info().Message("Migrating events {0}-{1} {2:N0} total {3:N0}/s...", errors.First().Id, errors.Last().Id, total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();

                    var upgradedErrors = JArray.FromObject(errors);
                    var ctx = new EventUpgraderContext(upgradedErrors, new Version(1, 5), true);
                    _eventUpgraderPluginManager.Upgrade(ctx);

                    var upgradedEvents = upgradedErrors.FromJson<PersistentEvent>(serializerSettings);

                    var stackIdsToCheck = upgradedEvents.Where(e => !knownStackIds.Contains(e.StackId)).Select(e => e.StackId).Distinct().ToArray();
                    if (stackIdsToCheck.Length > 0)
                        knownStackIds.AddRange(_eventRepository.ExistsByStackIds(stackIdsToCheck));
                        
                    upgradedEvents.ForEach(e => {
                        if (e.Date.UtcDateTime > DateTimeOffset.UtcNow.AddHours(1))
                            e.Date = DateTimeOffset.Now;

                       e.CreatedUtc = e.Date.ToUniversalTime().DateTime;

                        if (!knownStackIds.Contains(e.StackId)) {
                            // We haven't processed this stack id yet in this run. Check to see if this stack has already been imported..
                            e.IsFirstOccurrence = true;
                            knownStackIds.Add(e.StackId);
                        }

                        var request = e.GetRequestInfo();   
                        if (request != null)
                            e.AddRequestInfo(request.ApplyDataExclusions(RequestInfoPlugin.DefaultExclusions, RequestInfoPlugin.MAX_VALUE_LENGTH));

                        foreach (var ip in GetIpAddresses(e, request)) {
                            var location = _geoIpResolver.ResolveIp(ip);
                            if (location == null || !location.IsValid())
                                continue;

                            e.Geo = location.ToString();
                            break;
                        }

                        if (e.Type == Event.KnownTypes.NotFound && request != null) {
                            if (String.IsNullOrWhiteSpace(e.Source)) {
                                e.Message = null;
                                e.Source = request.GetFullPath(includeHttpMethod: true, includeHost: false, includeQueryString: false);
                            }

                            return;
                        }
                         
                        var error = e.GetError();
                        if (error == null) {
                            Debugger.Break();
                            Log.Error().Project(e.ProjectId).Message("Unable to get parse error model: {0}", e.Id).Write();
                            return;
                        }

                        var stackingTarget = error.GetStackingTarget();
                        if (stackingTarget != null && stackingTarget.Method != null && !String.IsNullOrEmpty(stackingTarget.Method.GetDeclaringTypeFullName()))
                            e.Source = stackingTarget.Method.GetDeclaringTypeFullName().Truncate(2000);

                        var signature = new ErrorSignature(error);
                        if (signature.SignatureInfo.Count <= 0)
                            return;

                        var targetInfo = new SettingsDictionary(signature.SignatureInfo);
                        if (stackingTarget != null && stackingTarget.Error != null && !targetInfo.ContainsKey("Message"))
                            targetInfo["Message"] = error.GetStackingTarget().Error.Message;

                        error.Data[Error.KnownDataKeys.TargetInfo] = targetInfo;
                    });

                    try {
                        _eventRepository.Add(upgradedEvents, sendNotification: false);
                    } catch (Exception) {
                        foreach (var persistentEvent in upgradedEvents) {
                            try {
                                _eventRepository.Add(persistentEvent, sendNotification: false);
                            } catch (Exception ex) {
                                //Debugger.Break();
                                Log.Error().Exception(ex).Message("An error occurred while migrating event '{0}': {1}", persistentEvent.Id, ex.Message).Write();
                            }
                        }
                    }

                    total += upgradedEvents.Count;
                    var lastId = upgradedEvents.Last().Id;
                    errors = errorCollection.Find(Query.GT(ErrorFieldNames.Id, ObjectId.Parse(lastId))).SetSortOrder(SortBy.Ascending(ErrorFieldNames.Id)).SetLimit(_batchSize).ToList();
                }
            }

            return JobResult.Success;
        }

        private IEnumerable<string> GetIpAddresses(PersistentEvent ev, RequestInfo request)  {
            if (request != null && !String.IsNullOrWhiteSpace(request.ClientIpAddress))
                yield return request.ClientIpAddress;

            var environmentInfo = ev.GetEnvironmentInfo();
            if (environmentInfo == null || String.IsNullOrWhiteSpace(environmentInfo.IpAddress))
                yield break;

            foreach (var ip in environmentInfo.IpAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                yield return ip;
        }

        #region Legacy mongo collections

        private MongoCollection<OldModels.Error> GetErrorCollection() {
            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.Error)))
                BsonClassMap.RegisterClassMap<OldModels.Error>(ConfigureErrorClassMap);

            return _mongoDatabase.GetCollection<OldModels.Error>("error");
        }

        private MongoCollection<Stack> GetErrorStackCollection() {
            if (!BsonClassMap.IsClassMapRegistered(typeof(Stack)))
                BsonClassMap.RegisterClassMap<Stack>(ConfigureErrorStackClassMap);

            return _mongoDatabase.GetCollection<Stack>("errorstack");
        }

        private static class ErrorFieldNames {
            public const string Id = "_id";
            public const string ProjectId = "pid";
            public const string ErrorStackId = "sid";
            public const string OrganizationId = "oid";
            public const string Message = "msg";
            public const string Type = "typ";
            public const string OccurrenceDate = "dt";
            public const string OccurrenceDate_UTC = "dt.0";
            public const string Tags = "tag";
            public const string UserEmail = "u-em";
            public const string UserName = "u-nm";
            public const string UserDescription = "u-dsc";
            public const string RequestInfo = "req";
            public const string ExceptionlessClientInfo = "cli";
            public const string Modules = "mod";
            public const string EnvironmentInfo = "env";
            public const string Code = "cod";
            public const string ExtendedData = "ext";
            public const string Inner = "inr";
            public const string StackTrace = "st";
            public const string TargetMethod = "meth";
            public const string UserAgent = "ag";
            public const string HttpMethod = "verb";
            public const string IsSecure = "sec";
            public const string Host = "hst";
            public const string Port = "prt";
            public const string Path = "url";
            public const string RequestInfo_Path = RequestInfo + "." + Path;
            public const string Referrer = "ref";
            public const string ClientIpAddress = "ip";
            public const string RequestInfo_ClientIpAddress = "ip";
            public const string Cookies = "cok";
            public const string PostData = "pst";
            public const string QueryString = "qry";
            public const string DeclaringNamespace = "ns";
            public const string DeclaringType = "dtyp";
            public const string Name = "nm";
            public const string GenericArguments = "arg";
            public const string Parameters = "prm";
            public const string Version = "ver";
            public const string InstallIdentifier = "iid";
            public const string InstallDate = "idt";
            public const string InstallDate_UTC = "idt.0";
            public const string IsSignatureTarget = "sig";
            public const string StartCount = "stc";
            public const string SubmitCount = "subc";
            public const string Platform = "pla";
            public const string SubmissionMethod = "sm";
            public const string ProcessorCount = "cpus";
            public const string TotalPhysicalMemory = "mem";
            public const string AvailablePhysicalMemory = "amem";
            public const string CommandLine = "cmd";
            public const string ProcessName = "pnm";
            public const string ProcessId = "pid";
            public const string ProcessMemorySize = "pmem";
            public const string ThreadName = "thr";
            public const string ThreadId = "tid";
            public const string Architecture = "arc";
            public const string OSName = "os";
            public const string OSVersion = "osv";
            public const string MachineName = "nm";
            public const string RuntimeVersion = "run";
            public const string IpAddress = "ip";
            public const string ModuleId = "mid";
            public const string TypeNamespace = "tns";
            public const string FileName = "fil";
            public const string LineNumber = "lin";
            public const string Column = "col";
            public const string IsEntry = "ent";
            public const string CreatedDate = "crt";
            public const string ModifiedDate = "mod";
            public const string IsFixed = "fix";
            public const string IsHidden = "hid";
        }

        private static void ConfigureErrorClassMap(BsonClassMap<OldModels.Error> cm) {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator()));
            cm.GetMemberMap(p => p.OrganizationId).SetElementName(ErrorFieldNames.OrganizationId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.ErrorStackId).SetElementName(ErrorFieldNames.ErrorStackId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.ProjectId).SetElementName(ErrorFieldNames.ProjectId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.OccurrenceDate).SetElementName(ErrorFieldNames.OccurrenceDate).SetSerializer(new UtcDateTimeOffsetSerializer());
            cm.GetMemberMap(c => c.Tags).SetElementName(ErrorFieldNames.Tags).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((OldModels.Error)obj).Tags.Any());
            cm.GetMemberMap(c => c.UserEmail).SetElementName(ErrorFieldNames.UserEmail).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.UserName).SetElementName(ErrorFieldNames.UserName).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.UserDescription).SetElementName(ErrorFieldNames.UserDescription).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.RequestInfo).SetElementName(ErrorFieldNames.RequestInfo).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.ExceptionlessClientInfo).SetElementName(ErrorFieldNames.ExceptionlessClientInfo).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Modules).SetElementName(ErrorFieldNames.Modules).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.EnvironmentInfo).SetElementName(ErrorFieldNames.EnvironmentInfo).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.IsFixed).SetElementName(ErrorFieldNames.IsFixed).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.IsHidden).SetElementName(ErrorFieldNames.IsHidden).SetIgnoreIfDefault(true);

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.ErrorInfo))) {
                BsonClassMap.RegisterClassMap<OldModels.ErrorInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Message).SetElementName(ErrorFieldNames.Message).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.Type).SetElementName(ErrorFieldNames.Type);
                    cmm.GetMemberMap(c => c.Code).SetElementName(ErrorFieldNames.Code);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(ErrorFieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((OldModels.ErrorInfo)obj).ExtendedData.Any());
                    cmm.GetMemberMap(c => c.Inner).SetElementName(ErrorFieldNames.Inner);
                    cmm.GetMemberMap(c => c.StackTrace).SetElementName(ErrorFieldNames.StackTrace).SetShouldSerializeMethod(obj => ((OldModels.ErrorInfo)obj).StackTrace.Any());
                    cmm.GetMemberMap(c => c.TargetMethod).SetElementName(ErrorFieldNames.TargetMethod).SetIgnoreIfNull(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.RequestInfo))) {
                BsonClassMap.RegisterClassMap<OldModels.RequestInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.UserAgent).SetElementName(ErrorFieldNames.UserAgent);
                    cmm.GetMemberMap(c => c.HttpMethod).SetElementName(ErrorFieldNames.HttpMethod);
                    cmm.GetMemberMap(c => c.IsSecure).SetElementName(ErrorFieldNames.IsSecure);
                    cmm.GetMemberMap(c => c.Host).SetElementName(ErrorFieldNames.Host);
                    cmm.GetMemberMap(c => c.Port).SetElementName(ErrorFieldNames.Port);
                    cmm.GetMemberMap(c => c.Path).SetElementName(ErrorFieldNames.Path);
                    cmm.GetMemberMap(c => c.Referrer).SetElementName(ErrorFieldNames.Referrer).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.ClientIpAddress).SetElementName(ErrorFieldNames.ClientIpAddress);
                    cmm.GetMemberMap(c => c.Cookies).SetElementName(ErrorFieldNames.Cookies).SetShouldSerializeMethod(obj => ((RequestInfo)obj).Cookies.Any());
                    cmm.GetMemberMap(c => c.PostData).SetElementName(ErrorFieldNames.PostData).SetShouldSerializeMethod(obj => ShouldSerializePostData(obj as RequestInfo));
                    cmm.GetMemberMap(c => c.QueryString).SetElementName(ErrorFieldNames.QueryString).SetShouldSerializeMethod(obj => ((OldModels.RequestInfo)obj).QueryString.Any());
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(ErrorFieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((OldModels.RequestInfo)obj).ExtendedData.Any());
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.ExceptionlessClientInfo))) {
                BsonClassMap.RegisterClassMap<OldModels.ExceptionlessClientInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Version).SetElementName(ErrorFieldNames.Version);
                    cmm.GetMemberMap(c => c.InstallIdentifier).SetElementName(ErrorFieldNames.InstallIdentifier);
                    cmm.GetMemberMap(c => c.InstallDate).SetElementName(ErrorFieldNames.InstallDate);
                    cmm.GetMemberMap(c => c.StartCount).SetElementName(ErrorFieldNames.StartCount);
                    cmm.GetMemberMap(c => c.SubmitCount).SetElementName(ErrorFieldNames.SubmitCount);
                    cmm.GetMemberMap(c => c.Platform).SetElementName(ErrorFieldNames.Platform);
                    cmm.GetMemberMap(c => c.SubmissionMethod).SetElementName(ErrorFieldNames.SubmissionMethod);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.EnvironmentInfo))) {
                BsonClassMap.RegisterClassMap<OldModels.EnvironmentInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.ProcessorCount).SetElementName(ErrorFieldNames.ProcessorCount);
                    cmm.GetMemberMap(c => c.TotalPhysicalMemory).SetElementName(ErrorFieldNames.TotalPhysicalMemory);
                    cmm.GetMemberMap(c => c.AvailablePhysicalMemory).SetElementName(ErrorFieldNames.AvailablePhysicalMemory);
                    cmm.GetMemberMap(c => c.CommandLine).SetElementName(ErrorFieldNames.CommandLine);
                    cmm.GetMemberMap(c => c.ProcessName).SetElementName(ErrorFieldNames.ProcessName);
                    cmm.GetMemberMap(c => c.ProcessId).SetElementName(ErrorFieldNames.ProcessId);
                    cmm.GetMemberMap(c => c.ProcessMemorySize).SetElementName(ErrorFieldNames.ProcessMemorySize);
                    cmm.GetMemberMap(c => c.ThreadName).SetElementName(ErrorFieldNames.ThreadName).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.ThreadId).SetElementName(ErrorFieldNames.ThreadId);
                    cmm.GetMemberMap(c => c.Architecture).SetElementName(ErrorFieldNames.Architecture);
                    cmm.GetMemberMap(c => c.OSName).SetElementName(ErrorFieldNames.OSName);
                    cmm.GetMemberMap(c => c.OSVersion).SetElementName(ErrorFieldNames.OSVersion);
                    cmm.GetMemberMap(c => c.MachineName).SetElementName(ErrorFieldNames.MachineName);
                    cmm.GetMemberMap(c => c.RuntimeVersion).SetElementName(ErrorFieldNames.RuntimeVersion);
                    cmm.GetMemberMap(c => c.IpAddress).SetElementName(ErrorFieldNames.IpAddress);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(ErrorFieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((OldModels.EnvironmentInfo)obj).ExtendedData.Any());
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.Method))) {
                BsonClassMap.RegisterClassMap<OldModels.Method>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.DeclaringNamespace).SetElementName(ErrorFieldNames.DeclaringNamespace);
                    cmm.GetMemberMap(c => c.DeclaringType).SetElementName(ErrorFieldNames.DeclaringType);
                    cmm.GetMemberMap(c => c.Name).SetElementName(ErrorFieldNames.Name);
                    cmm.GetMemberMap(c => c.ModuleId).SetElementName(ErrorFieldNames.ModuleId);
                    cmm.GetMemberMap(c => c.IsSignatureTarget).SetElementName(ErrorFieldNames.IsSignatureTarget);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(ErrorFieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((OldModels.Method)obj).ExtendedData.Any());
                    cmm.GetMemberMap(c => c.GenericArguments).SetElementName(ErrorFieldNames.GenericArguments).SetShouldSerializeMethod(obj => ((Method)obj).GenericArguments.Any());
                    cmm.GetMemberMap(c => c.Parameters).SetElementName(ErrorFieldNames.Parameters).SetShouldSerializeMethod(obj => ((OldModels.Method)obj).Parameters.Any());
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.Parameter))) {
                BsonClassMap.RegisterClassMap<OldModels.Parameter>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Name).SetElementName(ErrorFieldNames.Name);
                    cmm.GetMemberMap(c => c.Type).SetElementName(ErrorFieldNames.Type);
                    cmm.GetMemberMap(c => c.TypeNamespace).SetElementName(ErrorFieldNames.TypeNamespace);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(ErrorFieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((OldModels.Parameter)obj).ExtendedData.Any());
                    cmm.GetMemberMap(c => c.GenericArguments).SetElementName(ErrorFieldNames.GenericArguments).SetShouldSerializeMethod(obj => ((OldModels.Parameter)obj).GenericArguments.Any());
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.StackFrame))) {
                BsonClassMap.RegisterClassMap<OldModels.StackFrame>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.FileName).SetElementName(ErrorFieldNames.FileName).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.LineNumber).SetElementName(ErrorFieldNames.LineNumber).SetIgnoreIfDefault(true);
                    cmm.GetMemberMap(c => c.Column).SetElementName(ErrorFieldNames.Column).SetIgnoreIfDefault(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.Module))) {
                BsonClassMap.RegisterClassMap<OldModels.Module>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.ModuleId).SetElementName(ErrorFieldNames.ModuleId).SetIgnoreIfDefault(true);
                    cmm.GetMemberMap(c => c.Name).SetElementName(ErrorFieldNames.Name);
                    cmm.GetMemberMap(c => c.Version).SetElementName(ErrorFieldNames.Version).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.IsEntry).SetElementName(ErrorFieldNames.IsEntry).SetIgnoreIfDefault(true);
                    cmm.GetMemberMap(c => c.CreatedDate).SetElementName(ErrorFieldNames.CreatedDate);
                    cmm.GetMemberMap(c => c.ModifiedDate).SetElementName(ErrorFieldNames.ModifiedDate);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(ErrorFieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((OldModels.Module)obj).ExtendedData.Any());
                });
            }
        }

        private static class ErrorStackFieldNames {
            public const string Id = "_id";
            public const string ProjectId = "pid";
            public const string OrganizationId = "oid";
            public const string SignatureHash = "hash";
            public const string FirstOccurrence = "fst";
            public const string LastOccurrence = "lst";
            public const string TotalOccurrences = "tot";
            public const string SignatureInfo = "sig";
            public const string SignatureInfo_Path = "sig.Path";
            public const string FixedInVersion = "fix";
            public const string DateFixed = "fdt";
            public const string Title = "tit";
            public const string Description = "dsc";
            public const string IsHidden = "hid";
            public const string IsRegressed = "regr";
            public const string DisableNotifications = "dnot";
            public const string OccurrencesAreCritical = "crit";
            public const string References = "refs";
            public const string Tags = "tag";
        }

        private static void ConfigureErrorStackClassMap(BsonClassMap<Stack> cm) {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator()));
            cm.GetMemberMap(p => p.OrganizationId).SetElementName(ErrorStackFieldNames.OrganizationId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.ProjectId).SetRepresentation(BsonType.ObjectId).SetElementName(ErrorStackFieldNames.ProjectId);
            cm.GetMemberMap(c => c.SignatureHash).SetElementName(ErrorStackFieldNames.SignatureHash);
            cm.GetMemberMap(c => c.SignatureInfo).SetElementName(ErrorStackFieldNames.SignatureInfo);
            cm.GetMemberMap(c => c.FixedInVersion).SetElementName(ErrorStackFieldNames.FixedInVersion).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.DateFixed).SetElementName(ErrorStackFieldNames.DateFixed).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Title).SetElementName(ErrorStackFieldNames.Title);
            cm.GetMemberMap(c => c.TotalOccurrences).SetElementName(ErrorStackFieldNames.TotalOccurrences);
            cm.GetMemberMap(c => c.FirstOccurrence).SetElementName(ErrorStackFieldNames.FirstOccurrence);
            cm.GetMemberMap(c => c.LastOccurrence).SetElementName(ErrorStackFieldNames.LastOccurrence);
            cm.GetMemberMap(c => c.Description).SetElementName(ErrorStackFieldNames.Description).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.IsHidden).SetElementName(ErrorStackFieldNames.IsHidden).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.IsRegressed).SetElementName(ErrorStackFieldNames.IsRegressed).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.DisableNotifications).SetElementName(ErrorStackFieldNames.DisableNotifications).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.OccurrencesAreCritical).SetElementName(ErrorStackFieldNames.OccurrencesAreCritical).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.References).SetElementName(ErrorStackFieldNames.References).SetShouldSerializeMethod(obj => ((OldModels.ErrorStack)obj).References.Any());
            cm.GetMemberMap(c => c.Tags).SetElementName(ErrorStackFieldNames.Tags).SetShouldSerializeMethod(obj => ((OldModels.ErrorStack)obj).Tags.Any());
        }

        private static bool ShouldSerializePostData(RequestInfo requestInfo) {
            if (requestInfo == null)
                return false;

            if (requestInfo.PostData is Dictionary<string, string>)
                return ((Dictionary<string, string>)requestInfo.PostData).Any();

            return requestInfo.PostData != null;
        }

        #endregion
    }

    internal class StackMigrationRepository : StackRepository {
        public StackMigrationRepository(IElasticClient elasticClient, IEventRepository eventRepository, IValidator<Stack> validator = null) : base(elasticClient, eventRepository, validator, null, null) {
            EnableCache = false;
        }
        
        public Stack GetMostRecent() {
            return FindOne(new ElasticSearchOptions<Stack>().WithSort(s => s.OnField("_uid").Descending()));
        }
    }

    internal class EventMigrationRepository : EventRepository {
        public EventMigrationRepository(IElasticClient elasticClient, IValidator<PersistentEvent> validator = null) : base(elasticClient, validator, null) {
            EnableCache = false;
        }
        
        public PersistentEvent GetMostRecent() {
            return FindOne(new ElasticSearchOptions<PersistentEvent>().WithSort(s => s.OnField("_uid").Descending()));
        }

        public List<string> ExistsByStackIds(string[] stackIds) {
            var options = new ElasticSearchOptions<PersistentEvent>().WithStackIds(stackIds);
            var descriptor = new SearchDescriptor<PersistentEvent>().Filter(options.GetElasticSearchFilter()).Source(s => s.Include("stack_id")).Size(stackIds.Length);
            var results = _elasticClient.Search<PersistentEvent>(descriptor);
            return results.Documents.Select(e => e.StackId).ToList();
        }
    }
}