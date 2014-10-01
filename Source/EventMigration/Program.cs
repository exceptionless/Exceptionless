using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeSmith.Core.CommandLine;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
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
using Nest.Resolvers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SimpleInjector;
using OldModels = Exceptionless.EventMigration.Models;

namespace Exceptionless.EventMigration {
    internal class Program {
        private static int Main(string[] args) {
            OutputHeader();

            // TODO: Hook up nlog to write to the console.
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            try {
                var ca = new ConsoleArguments();
                if (Parser.ParseHelp(args)) {
                    OutputUsageHelp();
                    PauseIfDebug();
                    return 0;
                }

                if (!Parser.ParseArguments(args, ca, Console.Error.WriteLine)) {
                    OutputUsageHelp();
                    PauseIfDebug();
                    return 1;
                }

                Console.Clear();
                OutputHeader();
                const int BatchSize = 25;

                var container = CreateContainer();
                var searchclient = container.GetInstance<IElasticClient>();

                if (ca.DeleteExistingIndexes)
                    searchclient.DeleteIndex(i => i.AllIndices());

                var serializerSettings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore };
                serializerSettings.AddModelConverters();

                ISearchResponse<Stack> mostRecentStack = null;
                if (ca.Resume)
                    mostRecentStack = searchclient.Search<Stack>(d => d.AllIndices().Type(typeof(Stack)).SortDescending("_uid").Source(s => s.Include(e => e.Id)).Size(1));
                ISearchResponse<PersistentEvent> mostRecentEvent = null;
                if (ca.Resume)
                    mostRecentEvent = searchclient.Search<PersistentEvent>(d => d.AllIndices().Type(typeof(PersistentEvent)).SortDescending("_uid").Source(s => s.Include(e => e.Id)).Size(1));

                int total = 0;
                var stopwatch = new Stopwatch();
                if (!ca.SkipStacks) {
                    stopwatch.Start();
                    var errorStackCollection = GetErrorStackCollection(container);
                    var query = mostRecentStack != null && mostRecentStack.Total > 0 ? Query.GT(ErrorStackFieldNames.Id, ObjectId.Parse(mostRecentStack.Hits.First().Id)) : Query.Null;
                    var stacks = errorStackCollection.Find(query).SetSortOrder(SortBy.Ascending(ErrorStackFieldNames.Id)).SetLimit(BatchSize).ToList();
                    while (stacks.Count > 0) {
                        stacks.ForEach(s => {
                            s.Type = s.SignatureInfo != null && s.SignatureInfo.ContainsKey("HttpMethod") && s.SignatureInfo.ContainsKey("Path") ? "404" : "error";

                            if (s.Tags != null)
                                s.Tags.RemoveWhere(t => String.IsNullOrEmpty(t) || t.Length > 255);

                            if (s.Title != null && s.Title.Length > 1000)
                                s.Title = s.Title.Truncate(1000);
                        });

                        Console.SetCursorPosition(0, 4);
                        Console.WriteLine("Migrating stacks {0:N0} total {1:N0}/s...", total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0);
                        var response = searchclient.IndexMany(stacks, type: "stacks", index: ElasticSearchRepository<Stack>.StacksIndexName);
                        if (!response.IsValid)
                            Debugger.Break();

                        var lastId = stacks.Last().Id;
                        stacks = errorStackCollection.Find(Query.GT(ErrorStackFieldNames.Id, ObjectId.Parse(lastId))).SetSortOrder(SortBy.Ascending(ErrorStackFieldNames.Id)).SetLimit(BatchSize).ToList();
                        total += stacks.Count;
                    }
                }

                total = 0;
                stopwatch.Reset();
                if (!ca.SkipErrors) {
                    stopwatch.Start();
                    var eventUpgraderPluginManager = container.GetInstance<EventUpgraderPluginManager>();
                    var eventRepository = container.GetInstance<IEventRepository>();
                    var errorCollection = GetErrorCollection(container);
                    //var json1 = JsonExtensions.ToJson(errorCollection.FindOneById(ObjectId.Parse("80000000e2cc694bd029a952")), Formatting.Indented);
                    //var json2 = JsonExtensions.ToJson(errorCollection.FindOneById(ObjectId.Parse("80000000e2cc694bd029a953")), Formatting.Indented);
                    //var ctx2 = new EventUpgraderContext("[" + json1 + "," + json2 + "]", new Version(1, 5), true);
                    //eventUpgraderPluginManager.Upgrade(ctx2);
                    //var ev2 = ctx2.Documents.ToObject<List<PersistentEvent>>();
                    //eventRepository.Add(ev2.First());
                    //eventRepository.Add(ev2.Last());
                    var query = mostRecentEvent != null && mostRecentEvent.Total > 0 ? Query.GT(ErrorFieldNames.Id, ObjectId.Parse(mostRecentEvent.Hits.First().Id)) : Query.Null;
                    var errors = errorCollection.Find(query).SetSortOrder(SortBy.Ascending(ErrorFieldNames.Id)).SetLimit(BatchSize).ToList();
                    // TODO: When resuming, we need to get a list of existing stack ids from the events.
                    var knownStackIds = new List<string>();

                    while (errors.Count > 0) {
                        Console.SetCursorPosition(0, 5);
                        Console.WriteLine("Migrating events {0}-{1} {2:N0} total {3:N0}/s...", errors.First().Id, errors.Last().Id, total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0);

                        var events = JArray.FromObject(errors);
                        var ctx = new EventUpgraderContext(events, new Version(1, 5), true);
                        eventUpgraderPluginManager.Upgrade(ctx);

                        var ev = events.FromJson<PersistentEvent>(serializerSettings);
                        ev.ForEach(e => {
                            if (e.Date.UtcDateTime > DateTimeOffset.UtcNow.AddHours(1))
                                e.Date = DateTimeOffset.Now;

                            if (e.Type != Event.KnownTypes.Error)
                                return;

                            if (!knownStackIds.Contains(e.StackId)) {
                                e.IsFirstOccurrence = true;
                                knownStackIds.Add(e.StackId);
                            }

                            var request = e.GetRequestInfo();
                            if (request != null)
                                e.AddRequestInfo(request.ApplyDataExclusions(RequestInfoPlugin.DefaultExclusions, RequestInfoPlugin.MAX_VALUE_LENGTH));

                            var stacking = e.GetStackingTarget();
                            if (stacking != null && stacking.Method != null && !String.IsNullOrEmpty(stacking.Method.GetDeclaringTypeFullName()))
                                e.Source = stacking.Method.GetDeclaringTypeFullName().Truncate(2000);
                        });

                        try {
                            eventRepository.Add(ev);
                        } catch (Exception ex) {
                            Debugger.Break();
                        }

                        var lastId = ev.Last().Id;
                        errors = errorCollection.Find(Query.GT(ErrorFieldNames.Id, ObjectId.Parse(lastId))).SetSortOrder(SortBy.Ascending(ErrorFieldNames.Id)).SetLimit(BatchSize).ToList();
                        total += events.Count;
                    }
                }

                PauseIfDebug();
            } catch (FileNotFoundException e) {
                Console.Error.WriteLine("{0} ({1})", e.Message, e.FileName);
                PauseIfDebug();
                return 1;
            } catch (Exception e) {
                Console.Error.WriteLine(e.ToString());
                PauseIfDebug();
                return 1;
            }

            return 0;
        }

        public static Container CreateContainer() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Core.Bootstrapper>();

            return container;
        }

        private static void PauseIfDebug() {
            if (Debugger.IsAttached)
                Console.Read();
        }

        private static void OutputHeader() {
            Console.WriteLine("Exceptionless Event Migration v{0}", ThisAssembly.AssemblyInformationalVersion);
            Console.WriteLine("Copyright (c) 2012-{0} Exceptionless.  All rights reserved.", DateTime.Now.Year);
            Console.WriteLine();
        }

        private static void OutputUsageHelp() {
            Console.WriteLine("     - Exceptionless Event Migration -");
            Console.WriteLine();
            Console.WriteLine(Parser.ArgumentsUsage(typeof(ConsoleArguments)));
        }

        #region Legacy mongo collections

        private static MongoCollection<OldModels.Error> GetErrorCollection(Container container) {
            var database = container.GetInstance<MongoDatabase>();

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.Error)))
                BsonClassMap.RegisterClassMap<OldModels.Error>(ConfigureErrorClassMap);

            return database.GetCollection<OldModels.Error>("error");
        }

        private static MongoCollection<OldModels.ErrorStack> GetErrorStackCollection(Container container) {
            var database = container.GetInstance<MongoDatabase>();

            if (!BsonClassMap.IsClassMapRegistered(typeof(OldModels.ErrorStack)))
                BsonClassMap.RegisterClassMap<OldModels.ErrorStack>(ConfigureErrorStackClassMap);

            return database.GetCollection<OldModels.ErrorStack>("errorstack");
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

        private static void ConfigureErrorStackClassMap(BsonClassMap<OldModels.ErrorStack> cm) {
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

    internal class ErrorStackValidator : AbstractValidator<OldModels.ErrorStack> {
        public ErrorStackValidator() {
            RuleFor(s => s.OrganizationId).IsObjectId().WithMessage("Please specify a valid organization id.");
            RuleFor(s => s.ProjectId).IsObjectId().WithMessage("Please specify a valid project id.");
            RuleFor(s => s.Title).Length(1, 1000).When(s => s.Title != null).WithMessage("Title cannot be longer than 1000 characters.");
            RuleFor(s => s.SignatureHash).NotEmpty().WithMessage("Please specify a valid signature hash.");
            RuleFor(s => s.SignatureInfo).NotNull().WithMessage("Please specify a valid signature info.");
            RuleForEach(s => s.Tags).Length(1, 255).WithMessage("A tag cannot be longer than 255 characters.");
        }
    }

    public class EmptyCollectionContractResolver : ElasticContractResolver {
        public EmptyCollectionContractResolver(IConnectionSettingsValues connectionSettings) : base(connectionSettings) {}

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => (shouldSerialize == null || shouldSerialize(obj)) && !property.IsValueEmptyCollection(obj);
            return property;
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType) {
            if (objectType != typeof(DataDictionary) && objectType != typeof(SettingsDictionary))
                return base.CreateDictionaryContract(objectType);

            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
            contract.PropertyNameResolver = propertyName => propertyName;
            return contract;
        }

        protected override string ResolvePropertyName(string propertyName) {
            return propertyName.ToLowerUnderscoredWords();
        }
    }

}