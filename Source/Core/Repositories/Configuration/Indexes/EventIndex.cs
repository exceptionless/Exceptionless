using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class EventIndex : MonthlyIndex {
        public EventIndex(IElasticConfiguration configuration) : base(configuration, Settings.Current.AppScopePrefix + "events", 1) {
            DateFormat = "yyyyMM";
            MaxIndexAge = TimeSpan.FromDays(180);

            AddType(Event = new EventIndexType(this));
            AddAlias($"{Name}-today", TimeSpan.FromDays(1));
            AddAlias($"{Name}-last3days", TimeSpan.FromDays(7));
            AddAlias($"{Name}-last7days", TimeSpan.FromDays(7));
            AddAlias($"{Name}-last30days", TimeSpan.FromDays(30));
            AddAlias($"{Name}-last90days", TimeSpan.FromDays(90));
        }

        public EventIndexType Event { get; }
    }
    
    public class EventIndexType : MonthlyIndexType<PersistentEvent> {
        private const string COMMA_WHITESPACE_ANALYZER = "comma_whitespace";
        private const string EMAIL_ANALYZER = "email";
        private const string VERSION_INDEX_ANALYZER = "version_index";
        private const string VERSION_SEARCH_ANALYZER = "version_search";
        private const string WHITESPACE_LOWERCASE_ANALYZER = "whitespace_lower";
        private const string TYPENAME_ANALYZER = "typename";
        private const string STANDARDPLUS_ANALYZER = "standardplus";

        private const string EMAIL_TOKEN_FILTER = "email";
        private const string TYPENAME_TOKEN_FILTER = "typename";
        private const string VERSION_TOKEN_FILTER = "version";
        private const string VERSION_PAD1_TOKEN_FILTER = "version_pad1";
        private const string VERSION_PAD2_TOKEN_FILTER = "version_pad2";
        private const string VERSION_PAD3_TOKEN_FILTER = "version_pad3";
        const string VERSION_PAD4_TOKEN_FILTER = "version_pad4";

        public EventIndexType(EventIndex index) : base(index, "events", document => document.Date.UtcDateTime) {}
        
        public override CreateIndexDescriptor Configure(CreateIndexDescriptor idx) {
            return idx
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas)
                .Analysis(BuildAnalysis)
                .AddMapping<PersistentEvent>(BuildMapping);
        }

        private AnalysisDescriptor BuildAnalysis(AnalysisDescriptor ad) {
            return ad.Analyzers(bases => {
                bases.Add(COMMA_WHITESPACE_ANALYZER, new PatternAnalyzer { Pattern = @"[,\s]+" });
                bases.Add(EMAIL_ANALYZER, new CustomAnalyzer { Tokenizer = "keyword", Filter = new[] { EMAIL_TOKEN_FILTER, "lowercase", "unique" } });
                bases.Add(VERSION_INDEX_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new[] { VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, VERSION_TOKEN_FILTER, "lowercase", "unique" } });
                bases.Add(VERSION_SEARCH_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new[] { VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, "lowercase" } });
                bases.Add(WHITESPACE_LOWERCASE_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new[] { "lowercase" } });
                bases.Add(TYPENAME_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new[] { TYPENAME_TOKEN_FILTER, "lowercase", "unique" } });
                bases.Add(STANDARDPLUS_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new[] { "standard", TYPENAME_TOKEN_FILTER, "lowercase", "stop", "unique" } });
                return bases;
            }).TokenFilters(bases => {
                bases.Add(EMAIL_TOKEN_FILTER, new PatternCaptureTokenFilter { Patterns = new[] { @"(\w+)", @"(\p{L}+)", @"(\d+)", @"(.+)@", @"@(.+)" } });
                bases.Add(TYPENAME_TOKEN_FILTER, new PatternCaptureTokenFilter { Patterns = new[] { @"\.(\w+)" } });
                bases.Add(VERSION_TOKEN_FILTER, new PatternCaptureTokenFilter { Patterns = new[] { @"^(\d+)\.", @"^(\d+\.\d+)", @"^(\d+\.\d+\.\d+)" } });
                bases.Add(VERSION_PAD1_TOKEN_FILTER, new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{1})(?=\.|-|$)", Replacement = @"$10000$2" });
                bases.Add(VERSION_PAD2_TOKEN_FILTER, new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{2})(?=\.|-|$)", Replacement = @"$1000$2" });
                bases.Add(VERSION_PAD3_TOKEN_FILTER, new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{3})(?=\.|-|$)", Replacement = @"$100$2" });
                bases.Add(VERSION_PAD4_TOKEN_FILTER, new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{4})(?=\.|-|$)", Replacement = @"$10$2" });
                return bases;
            });
        }

        public override PutMappingDescriptor<PersistentEvent> BuildMapping(PutMappingDescriptor<PersistentEvent> map) {
            return map
                .Type(Name)
                .Dynamic(DynamicMappingOption.Ignore)
                .DynamicTemplates(dt => dt.Add(t => t.Name("idx_reference").Match("*-r").Mapping(m => m.Generic(f => f.Type("string").Index("not_analyzed")))))
                .IncludeInAll(false)
                .DisableSizeField(false) // Change to Size Field
                .Transform(t => t.Script(FLATTEN_ERRORS_SCRIPT).Language(ScriptLang.Groovy))
                .AllField(a => a.IndexAnalyzer(STANDARDPLUS_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER))
                .Properties(p => p
                    .Date(f => f.Name(e => e.CreatedUtc).IndexName(Fields.CreatedUtc))
                    .String(f => f.Name(e => e.Id).IndexName(Fields.Id).Index(FieldIndexOption.NotAnalyzed).IncludeInAll())
                    .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ProjectId).IndexName(Fields.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.StackId).IndexName(Fields.StackId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.ReferenceId).IndexName(Fields.ReferenceId).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Type).IndexName(Fields.Type).Index(FieldIndexOption.NotAnalyzed))
                    .String(f => f.Name(e => e.Source).IndexName(Fields.Source).Index(FieldIndexOption.Analyzed).IncludeInAll())
                    .Date(f => f.Name(e => e.Date).IndexName(Fields.Date))
                    .String(f => f.Name(e => e.Message).IndexName(Fields.Message).Index(FieldIndexOption.Analyzed).IncludeInAll())
                    .String(f => f.Name(e => e.Tags).IndexName(Fields.Tags).Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.2))
                    .GeoPoint(f => f.Name(e => e.Geo).Name(Fields.Geo).IndexLatLon())
                    .Number(f => f.Name(e => e.Value).IndexName(Fields.Value))
                    .Number(f => f.Name(e => e.Count).IndexName(Fields.Count))
                    .Boolean(f => f.Name(e => e.IsFirstOccurrence).IndexName(Fields.IsFirstOccurrence))
                    .Boolean(f => f.Name(e => e.IsFixed).IndexName(Fields.IsFixed))
                    .Boolean(f => f.Name(e => e.IsHidden).IndexName(Fields.IsHidden))
                    .Object<object>(f => f.Name(Fields.IDX).Dynamic())
                    .Object<DataDictionary>(f => f.Name(e => e.Data).Path("just_name").Properties(p2 => p2
                        .String(f2 => f2.Name(Event.KnownDataKeys.Version).IndexName(Fields.Version).Index(FieldIndexOption.Analyzed).Analyzer(VERSION_INDEX_ANALYZER).SearchAnalyzer(VERSION_SEARCH_ANALYZER))
                        .String(f2 => f2.Name(Event.KnownDataKeys.Level).IndexName(Fields.Level).Index(FieldIndexOption.Analyzed))
                        .String(f2 => f2.Name(Event.KnownDataKeys.SubmissionMethod).IndexName(Fields.SubmissionMethod).Index(FieldIndexOption.Analyzed))
                            .Object<Location>(f2 => f2.Name(Event.KnownDataKeys.Location).Path("just_name").Properties(p3 => p3
                            .String(f3 => f3.Name(r => r.Country).IndexName(Fields.LocationCountry).Index(FieldIndexOption.NotAnalyzed))
                            .String(f3 => f3.Name(r => r.Level1).IndexName(Fields.LocationLevel1).Index(FieldIndexOption.NotAnalyzed))
                            .String(f3 => f3.Name(r => r.Level2).IndexName(Fields.LocationLevel2).Index(FieldIndexOption.NotAnalyzed))
                            .String(f3 => f3.Name(r => r.Locality).IndexName(Fields.LocationLocality).Index(FieldIndexOption.NotAnalyzed))))
                        .Object<RequestInfo>(f2 => f2.Name(Event.KnownDataKeys.RequestInfo).Path("just_name").Properties(p3 => p3
                            .String(f3 => f3.Name(r => r.ClientIpAddress).IndexName(Fields.IpAddress).Index(FieldIndexOption.Analyzed).IncludeInAll().Analyzer(COMMA_WHITESPACE_ANALYZER))
                            .String(f3 => f3.Name(r => r.UserAgent).IndexName(Fields.RequestUserAgent).Index(FieldIndexOption.Analyzed))
                            .String(f3 => f3.Name(r => r.Path).IndexName(Fields.RequestPath).Index(FieldIndexOption.Analyzed).IncludeInAll())
                            .Object<DataDictionary>(f3 => f3.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                .String(f4 => f4.Name(RequestInfo.KnownDataKeys.Browser).IndexName(Fields.Browser).Index(FieldIndexOption.Analyzed)
                                    .Fields(fields => fields.String(ss => ss.Name(Fields.BrowserRaw).Index(FieldIndexOption.NotAnalyzed))))
                                .String(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserVersion).IndexName(Fields.BrowserVersion).Index(FieldIndexOption.Analyzed)
                                    .Fields(fields => fields.String(ss => ss.Name(Fields.BrowserVersionRaw).Index(FieldIndexOption.NotAnalyzed))))
                                .String(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserMajorVersion).IndexName(Fields.BrowserMajorVersion).Index(FieldIndexOption.NotAnalyzed))
                                .String(f4 => f4.Name(RequestInfo.KnownDataKeys.Device).IndexName(Fields.Device).Index(FieldIndexOption.Analyzed)
                                    .Fields(fields => fields.String(ss => ss.Name(Fields.DeviceRaw).Index(FieldIndexOption.NotAnalyzed))))
                                .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OS).IndexName(Fields.OperatingSystem).Index(FieldIndexOption.Analyzed)
                                    .Fields(fields => fields.String(ss => ss.Name(Fields.OperatingSystemRaw).Index(FieldIndexOption.NotAnalyzed))))
                                .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OSVersion).IndexName(Fields.OperatingSystemVersion).Index(FieldIndexOption.Analyzed)
                                    .Fields(fields => fields.String(ss => ss.Name(Fields.OperatingSystemVersionRaw).Index(FieldIndexOption.NotAnalyzed))))
                                .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OSMajorVersion).IndexName(Fields.OperatingSystemMajorVersion).Index(FieldIndexOption.NotAnalyzed))
                                .Boolean(f4 => f4.Name(RequestInfo.KnownDataKeys.IsBot).IndexName(Fields.RequestIsBot))))))
                        .Object<Error>(f2 => f2.Name(Event.KnownDataKeys.Error).Path("just_name").Properties(p3 => p3
                            .String(f3 => f3.Name("all_codes").IndexName(Fields.ErrorCode).Index(FieldIndexOption.NotAnalyzed).Analyzer("whitespace").IncludeInAll().Boost(1.1))
                            .String(f3 => f3.Name("all_messages").IndexName(Fields.ErrorMessage).Index(FieldIndexOption.Analyzed).IncludeInAll())
                            .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Path("just_name").Properties(p5 => p5
                                    .String(f6 => f6.Name("ExceptionType").IndexName(Fields.ErrorTargetType).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2)
                                        .Fields(fields => fields.String(ss => ss.Name(Fields.ErrorTargetTypeRaw).Index(FieldIndexOption.NotAnalyzed))))
                                    .String(f6 => f6.Name("Method").IndexName(Fields.ErrorTargetMethod).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2))))))
                            .String(f3 => f3.Name("all_types").IndexName(Fields.ErrorType).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1))))
                        .Object<SimpleError>(f2 => f2.Name(Event.KnownDataKeys.SimpleError).Path("just_name").Properties(p3 => p3
                            .String(f3 => f3.Name("all_messages").IndexName(Fields.ErrorMessage).Index(FieldIndexOption.Analyzed).IncludeInAll())
                            .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Path("just_name").Properties(p5 => p5
                                    .String(f6 => f6.Name("ExceptionType").IndexName(Fields.ErrorTargetType).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2))))))
                            .String(f3 => f3.Name("all_types").IndexName(Fields.ErrorType).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1))))
                        .Object<EnvironmentInfo>(f2 => f2.Name(Event.KnownDataKeys.EnvironmentInfo).Path("just_name").Properties(p3 => p3
                            .String(f3 => f3.Name(r => r.IpAddress).IndexName(Fields.IpAddress).Index(FieldIndexOption.Analyzed).IncludeInAll().Analyzer(COMMA_WHITESPACE_ANALYZER))
                            .String(f3 => f3.Name(r => r.MachineName).IndexName(Fields.MachineName).Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.1))
                            .String(f3 => f3.Name(r => r.OSName).IndexName(Fields.OperatingSystem).Index(FieldIndexOption.Analyzed))
                            .String(f3 => f3.Name(r => r.Architecture).IndexName(Fields.MachineArchitecture).Index(FieldIndexOption.NotAnalyzed))))
                        .Object<UserDescription>(f2 => f2.Name(Event.KnownDataKeys.UserDescription).Path("just_name").Properties(p3 => p3
                            .String(f3 => f3.Name(r => r.Description).IndexName(Fields.UserDescription).Index(FieldIndexOption.Analyzed).IncludeInAll())
                            .String(f3 => f3.Name(r => r.EmailAddress).IndexName(Fields.UserEmail).Index(FieldIndexOption.Analyzed).Analyzer(EMAIL_ANALYZER).SearchAnalyzer("simple").IncludeInAll().Boost(1.1))))
                        .Object<UserInfo>(f2 => f2.Name(Event.KnownDataKeys.UserInfo).Path("just_name").Properties(p3 => p3
                            .String(f3 => f3.Name(r => r.Identity).IndexName(Fields.User).Index(FieldIndexOption.Analyzed).Analyzer(EMAIL_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1)
                                .Fields(fields => fields.String(ss => ss.Name(Fields.UserRaw).Index(FieldIndexOption.NotAnalyzed))))
                            .String(f3 => f3.Name(r => r.Name).IndexName(Fields.UserName).Index(FieldIndexOption.Analyzed).IncludeInAll())))))
                );
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
            public const string CreatedUtc = "created";
            public const string OrganizationId = "organization";
            public const string ProjectId = "project";
            public const string StackId = "stack";
            public const string Id = "id";
            public const string ReferenceId = "reference";
            public const string Date = "date";
            public const string Type = "type";
            public const string Source = "source";
            public const string Message = "message";
            public const string Tags = "tag";
            public const string Geo = "geo";
            public const string Value = "value";
            public const string Count = "count";
            public const string IsFirstOccurrence = "first";
            public const string IsFixed = "fixed";
            public const string IsHidden = "hidden";
            public const string IDX = "idx";

            public const string Version = "version";
            public const string Level = "level";
            public const string SubmissionMethod = "submission";

            public const string IpAddress = "ip";

            public const string RequestUserAgent = "useragent";
            public const string RequestPath = "path";

            public const string Browser = "browser";
            public const string BrowserRaw = "browser.raw";
            public const string BrowserVersion = "browser.version";
            public const string BrowserVersionRaw = "browser.version.raw";
            public const string BrowserMajorVersion = "browser.major";
            public const string RequestIsBot = "bot";

            public const string Device = "device";
            public const string DeviceRaw = "device.raw";

            public const string OperatingSystem = "os";
            public const string OperatingSystemRaw = "os.raw";
            public const string OperatingSystemVersion = "os.version";
            public const string OperatingSystemVersionRaw = "os.version.raw";
            public const string OperatingSystemMajorVersion = "os.major";

            public const string MachineName = "machine";
            public const string MachineArchitecture = "architecture";

            public const string User = "user";
            public const string UserRaw = "user.raw";
            public const string UserName = "user.name";
            public const string UserEmail = "user.email";
            public const string UserDescription = "user.description";

            public const string LocationCountry = "country";
            public const string LocationLevel1 = "level1";
            public const string LocationLevel2 = "level2";
            public const string LocationLocality = "locality";

            public const string ErrorCode = "error.code";
            public const string ErrorType = "error.type";
            public const string ErrorMessage = "error.message";
            public const string ErrorTargetType = "error.targettype";
            public const string ErrorTargetTypeRaw = "error.targettype.raw";
            public const string ErrorTargetMethod = "error.targetmethod";
        }
    }
}
