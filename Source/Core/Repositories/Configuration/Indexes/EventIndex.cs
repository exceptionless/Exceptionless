using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class EventIndex : ITemplatedElasticIndex {
        public int Version => 1;
        public static string Alias => Settings.Current.AppScopePrefix + "events";
        public string AliasName => Alias;
        public string VersionedName => String.Concat(AliasName, "-v", Version);

        public IDictionary<Type, IndexType> GetIndexTypes() {
            return new Dictionary<Type, IndexType> {
                { typeof(PersistentEvent), new IndexType { Name = "events" } }
            };
        }

        public CreateIndexDescriptor CreateIndex(CreateIndexDescriptor idx) {
            throw new NotImplementedException();
        }

        public PutIndexTemplateDescriptor CreateTemplate(PutIndexTemplateDescriptor template) {
            return template
                .Template(VersionedName + "-*")
                .Settings(s => s.Analysis(a => BuildAnalysisSettings()))
                .Mappings(maps => maps
                    .Map<PersistentEvent>(map => map
                        .Dynamic(DynamicMapping.Ignore)
                        .DynamicTemplates(dt => dt.DynamicTemplate("idx_reference", t => t.Match("*-r").Mapping(m => m.String(s => s.Index(FieldIndexOption.NotAnalyzed)))))
                        .AllField(a => a.Enabled(false).Analyzer("standardplus").SearchAnalyzer("whitespace_lower"))
                        .DisableSizeField(false) // Change to Size Field
                        .Transform(t => t.Add(a => a.Script(FLATTEN_ERRORS_SCRIPT).Language(ScriptLang.Groovy)))
                        .Properties(p => p
                            .Date(f => f.Name(e => e.CreatedUtc).IndexName(Fields.PersistentEvent.CreatedUtc))
                            .String(f => f.Name(e => e.Id).IndexName(Fields.PersistentEvent.Id).Index(FieldIndexOption.NotAnalyzed).IncludeInAll())
                            .String(f => f.Name(e => e.OrganizationId).IndexName("organization").Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.ProjectId).IndexName("project").Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.StackId).IndexName("stack").Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.ReferenceId).IndexName("reference").Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.Type).IndexName(Fields.PersistentEvent.Type).Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.Source).IndexName("source").Index(FieldIndexOption.Analyzed).IncludeInAll())
                            .Date(f => f.Name(e => e.Date).IndexName(Fields.PersistentEvent.Date))
                            .String(f => f.Name(e => e.Message).IndexName("message").Index(FieldIndexOption.Analyzed).IncludeInAll())
                            .String(f => f.Name(e => e.Tags).IndexName("tag").Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.2))
                            .GeoPoint(f => f.Name(e => e.Geo).LatLon())
                            .Number(f => f.Name(e => e.Value).IndexName("value"))
                            .Boolean(f => f.Name(e => e.IsFirstOccurrence).IndexName("first"))
                            .Boolean(f => f.Name(e => e.IsFixed).IndexName("fixed"))
                            .Boolean(f => f.Name(e => e.IsHidden).IndexName("hidden"))
                            .Object<object>(f => f.Name("idx").Dynamic())
                            .Object<DataDictionary>(f => f.Name(e => e.Data).Path("just_name").Properties(p2 => p2
                                .String(f2 => f2.Name(Event.KnownDataKeys.Version).IndexName("version").Index(FieldIndexOption.Analyzed).Analyzer("version_index").SearchAnalyzer("version_search"))
                                .String(f2 => f2.Name(Event.KnownDataKeys.Level).IndexName("level").Index(FieldIndexOption.Analyzed))
                                .String(f2 => f2.Name(Event.KnownDataKeys.SubmissionMethod).IndexName("submission").Index(FieldIndexOption.Analyzed))
                                 .Object<Location>(f2 => f2.Name(Event.KnownDataKeys.Location).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.Country).IndexName(Fields.PersistentEvent.LocationCountry).Index(FieldIndexOption.NotAnalyzed))
                                    .String(f3 => f3.Name(r => r.Country).IndexName(Fields.PersistentEvent.LocationLevel1).Index(FieldIndexOption.NotAnalyzed))
                                    .String(f3 => f3.Name(r => r.Country).IndexName(Fields.PersistentEvent.LocationLevel2).Index(FieldIndexOption.NotAnalyzed))
                                    .String(f3 => f3.Name(r => r.Country).IndexName(Fields.PersistentEvent.LocationLocality).Index(FieldIndexOption.NotAnalyzed))))
                                .Object<RequestInfo>(f2 => f2.Name(Event.KnownDataKeys.RequestInfo).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.ClientIpAddress).IndexName(Fields.PersistentEvent.IpAddress).Index(FieldIndexOption.Analyzed).IncludeInAll().Analyzer("comma_whitespace"))
                                    .String(f3 => f3.Name(r => r.UserAgent).IndexName("useragent").Index(FieldIndexOption.Analyzed))
                                    .String(f3 => f3.Name(r => r.Path).IndexName("path").Index(FieldIndexOption.Analyzed).IncludeInAll())
                                    .Object<DataDictionary>(f3 => f3.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.Browser).IndexName("browser").Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name("browser.raw").Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserVersion).IndexName("browser.version").Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name("browser.version.raw").Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserMajorVersion).IndexName("browser.major").Index(FieldIndexOption.NotAnalyzed))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.Device).IndexName("device").Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name("device.raw").Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OS).IndexName("os").Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name("os.raw").Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OSVersion).IndexName("os.version").Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name("os.version.raw").Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OSMajorVersion).IndexName("os.major").Index(FieldIndexOption.NotAnalyzed))
                                        .Boolean(f4 => f4.Name(RequestInfo.KnownDataKeys.IsBot).IndexName("bot"))))))
                                .Object<Error>(f2 => f2.Name(Event.KnownDataKeys.Error).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name("all_codes").IndexName("error.code").Index(FieldIndexOption.NotAnalyzed).Analyzer("whitespace").IncludeInAll().Boost(1.1))
                                    .String(f3 => f3.Name("all_messages").IndexName("error.message").Index(FieldIndexOption.Analyzed).IncludeInAll())
                                    .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                        .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Path("just_name").Properties(p5 => p5
                                            .String(f6 => f6.Name("ExceptionType").IndexName("error.targettype").Index(FieldIndexOption.Analyzed).Analyzer("typename").SearchAnalyzer("whitespace_lower").IncludeInAll().Boost(1.2)
                                                .Fields(fields => fields.String(ss => ss.Name("error.targettype.raw").Index(FieldIndexOption.NotAnalyzed))))
                                            .String(f6 => f6.Name("Method").IndexName("error.targetmethod").Index(FieldIndexOption.Analyzed).Analyzer("typename").SearchAnalyzer("whitespace_lower").IncludeInAll().Boost(1.2))))))
                                    .String(f3 => f3.Name("all_types").IndexName("error.type").Index(FieldIndexOption.Analyzed).Analyzer("typename").SearchAnalyzer("whitespace_lower").IncludeInAll().Boost(1.1))))
                                .Object<SimpleError>(f2 => f2.Name(Event.KnownDataKeys.SimpleError).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name("all_messages").IndexName("error.message").Index(FieldIndexOption.Analyzed).IncludeInAll())
                                    .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                        .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Path("just_name").Properties(p5 => p5
                                            .String(f6 => f6.Name("ExceptionType").IndexName("error.targettype").Index(FieldIndexOption.Analyzed).Analyzer("typename").SearchAnalyzer("whitespace_lower").IncludeInAll().Boost(1.2))))))
                                    .String(f3 => f3.Name("all_types").IndexName("error.type").Index(FieldIndexOption.Analyzed).Analyzer("typename").SearchAnalyzer("whitespace_lower").IncludeInAll().Boost(1.1))))
                                .Object<EnvironmentInfo>(f2 => f2.Name(Event.KnownDataKeys.EnvironmentInfo).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.IpAddress).IndexName(Fields.PersistentEvent.IpAddress).Index(FieldIndexOption.Analyzed).IncludeInAll().Analyzer("comma_whitespace"))
                                    .String(f3 => f3.Name(r => r.MachineName).IndexName("machine").Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.1))
                                    .String(f3 => f3.Name(r => r.OSName).IndexName("os").Index(FieldIndexOption.Analyzed))
                                    .String(f3 => f3.Name(r => r.Architecture).IndexName("architecture").Index(FieldIndexOption.NotAnalyzed))))
                                .Object<UserDescription>(f2 => f2.Name(Event.KnownDataKeys.UserDescription).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.Description).IndexName("user.description").Index(FieldIndexOption.Analyzed).IncludeInAll())
                                    .String(f3 => f3.Name(r => r.EmailAddress).IndexName(Fields.PersistentEvent.UserEmail).Index(FieldIndexOption.Analyzed).Analyzer("email").SearchAnalyzer("simple").IncludeInAll().Boost(1.1))))
                                .Object<UserInfo>(f2 => f2.Name(Event.KnownDataKeys.UserInfo).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.Identity).IndexName(Fields.PersistentEvent.User).Index(FieldIndexOption.Analyzed).Analyzer("email").SearchAnalyzer("whitespace_lower").IncludeInAll().Boost(1.1)
                                        .Fields(fields => fields.String(ss => ss.Name(Fields.PersistentEvent.UserRaw).Index(FieldIndexOption.NotAnalyzed))))
                                    .String(f3 => f3.Name(r => r.Name).IndexName(Fields.PersistentEvent.UserName).Index(FieldIndexOption.Analyzed).IncludeInAll())))))
                        )));
        }

        private IAnalysis BuildAnalysisSettings() {
            return new Analysis {
                Analyzers = new Analyzers {
                    { "comma_whitespace", new PatternAnalyzer { Pattern = @"[,\s]+" } },
                    { "email", new CustomAnalyzer { Tokenizer = "keyword", Filter = new [] { "email", "lowercase", "unique" } } },
                    { "version_index", new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { "version_pad1", "version_pad2", "version_pad3", "version_pad4", "version", "lowercase", "unique" } } },
                    { "version_search", new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { "version_pad1", "version_pad2", "version_pad3", "version_pad4", "lowercase" } } },
                    { "whitespace_lower", new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { "lowercase" } } },
                    { "typename", new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { "typename", "lowercase", "unique" } } },
                    { "standardplus", new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { "standard", "typename", "lowercase", "stop", "unique" } } }
                },
                TokenFilters = new TokenFilters {
                    { "email", new PatternCaptureTokenFilter { Patterns = new[] { @"(\w+)", @"(\p{L}+)", @"(\d+)", @"(.+)@", @"@(.+)" } } },
                    { "typename", new PatternCaptureTokenFilter { Patterns = new[] { @"\.(\w+)" } } },
                    { "version", new PatternCaptureTokenFilter { Patterns = new[] { @"^(\d+)\.", @"^(\d+\.\d+)", @"^(\d+\.\d+\.\d+)" } } },
                    { "version_pad1", new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{1})(?=\.|$)", Replacement = @"$10000$2" } },
                    { "version_pad2", new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{2})(?=\.|$)", Replacement = @"$1000$2" } },
                    { "version_pad3", new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{3})(?=\.|$)", Replacement = @"$100$2" } },
                    { "version_pad4", new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{4})(?=\.|$)", Replacement = @"$10$2" } }
                }
            };
        }

        const string FLATTEN_ERRORS_SCRIPT = @"
if (!ctx._source.containsKey('data') || !(ctx._source.data.containsKey('@error') || ctx._source.data.containsKey('@simple_error')))
    return

def types = []
def messages = []
def codes = []
def err = ctx._source.data.containsKey('@error') ? ctx._source.data['@error'] : ctx._source.data['@simple_error']
def curr = err
while (curr != null) {
    if (curr.containsKey('type'))
        types.add(curr.type)
    if (curr.containsKey('message'))
        messages.add(curr.message)
    if (curr.containsKey('code'))
        codes.add(curr.code)
    curr = curr.inner
}

err['all_types'] = types.join(' ')
err['all_messages'] = messages.join(' ')
err['all_codes'] = codes.join(' ')";

        public class Fields {
            public class PersistentEvent {
                public const string CreatedUtc = "created";
                public const string Id = "id";
                public const string Date = "date";
                public const string Type = "type";
                public const string IpAddress = "ip";
                public const string User = "user";
                public const string UserRaw = "user.raw";
                public const string UserName = "user.name";
                public const string UserEmail = "user.email";
                public const string LocationCountry = "country";
                public const string LocationLevel1 = "level1";
                public const string LocationLevel2 = "level2";
                public const string LocationLocality = "locality";
            }
        }
    }
}
