using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class EventIndex : MonthlyIndex {
        public EventIndex(IElasticConfiguration configuration) : base(configuration, Settings.Current.AppScopePrefix + "events", 1) {
            DateFormat = "yyyyMM";
            MaxIndexAge = TimeSpan.FromDays(180);
            DiscardExpiredIndexes = false;

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
            return base.Configure(idx)
                .Settings(s => s
                    .Analysis(BuildAnalysis)
                    .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                    .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas));
        }

        private AnalysisDescriptor BuildAnalysis(AnalysisDescriptor ad) {
            return ad
            .Analyzers(a => a
                .Pattern(COMMA_WHITESPACE_ANALYZER, p => p.Pattern(@"[,\s]+"))
                .Custom(EMAIL_ANALYZER, c => c.Filters(EMAIL_TOKEN_FILTER, "lowercase", "unique").Tokenizer("keyword"))
                .Custom(VERSION_INDEX_ANALYZER, c => c.Filters(VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, VERSION_TOKEN_FILTER, "lowercase", "unique").Tokenizer("whitespace"))
                .Custom(VERSION_SEARCH_ANALYZER, c => c.Filters(VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, "lowercase").Tokenizer("whitespace"))
                .Custom(WHITESPACE_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("whitespace"))
                .Custom(TYPENAME_ANALYZER, c => c.Filters(TYPENAME_TOKEN_FILTER, "lowercase", "unique").Tokenizer("whitespace"))
                .Custom(STANDARDPLUS_ANALYZER, c => c.Filters("standard", TYPENAME_TOKEN_FILTER, "lowercase", "stop", "unique").Tokenizer("whitespace")))
            .TokenFilters(f => f
                .PatternCapture(EMAIL_TOKEN_FILTER, p => p.Patterns(@"(\w+)", @"(\p{L}+)", @"(\d+)", "(.+)@", "@(.+)"))
                .PatternCapture(TYPENAME_TOKEN_FILTER, p => p.Patterns(@"\.(\w+)"))
                .PatternCapture(VERSION_TOKEN_FILTER, p => p.Patterns(@"^(\d+)\.", @"^(\d+\.\d+)", @"^(\d+\.\d+\.\d+)"))
                .PatternReplace(VERSION_PAD1_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{1})(?=\.|-|$)").Replacement("$10000$2"))
                .PatternReplace(VERSION_PAD2_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{2})(?=\.|-|$)").Replacement("$1000$2"))
                .PatternReplace(VERSION_PAD3_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{3})(?=\.|-|$)").Replacement("$100$2"))
                .PatternReplace(VERSION_PAD4_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{4})(?=\.|-|$)").Replacement("$10$2")));
        }

        public override TypeMappingDescriptor<PersistentEvent> BuildMapping(TypeMappingDescriptor<PersistentEvent> map) {
            return base.BuildMapping(map)
                .Dynamic(false)
                .DynamicTemplates(dt => dt.DynamicTemplate("idx_reference", t => t.Match("*-r").Mapping(m => m.Keyword(s => s.IgnoreAbove(256)))))
                .DisableSizeField(false) // Change to Size Field
                //.Transform(t => t.Add(a => a.Script(FLATTEN_ERRORS_SCRIPT).Language(ScriptLang.Groovy)))
                .AllField(a => a.Enabled(false).Analyzer(STANDARDPLUS_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER))
                .Properties(p => p
                    .Date(f => f.Name(e => e.CreatedUtc).Alias(Alias.CreatedUtc))
                    .Keyword(f => f.Name(e => e.Id).Alias(Alias.Id).IncludeInAll())
                    .Keyword(f => f.Name(e => e.OrganizationId).Alias(Alias.OrganizationId))
                    .Keyword(f => f.Name(e => e.ProjectId).Alias(Alias.ProjectId))
                    .Keyword(f => f.Name(e => e.StackId).Alias(Alias.StackId))
                    .Keyword(f => f.Name(e => e.ReferenceId).Alias(Alias.ReferenceId))
                    .Keyword(f => f.Name(e => e.Type).Alias(Alias.Type))
                    .Text(f => f.Name(e => e.Source).Alias(Alias.Source).IncludeInAll().AddKeywordField())
                    .Date(f => f.Name(e => e.Date).Alias(Alias.Date))
                    .Text(f => f.Name(e => e.Message).Alias(Alias.Message).IncludeInAll())
                    .Text(f => f.Name(e => e.Tags).Alias(Alias.Tags).IncludeInAll().Boost(1.2).AddKeywordField())
                    .GeoPoint(f => f.Name(e => e.Geo).Alias(Alias.Geo))
                    .Number(f => f.Name(e => e.Value).Alias(Alias.Value))
                    .Number(f => f.Name(e => e.Count).Alias(Alias.Count))
                    .Boolean(f => f.Name(e => e.IsFirstOccurrence).Alias(Alias.IsFirstOccurrence))
                    .Boolean(f => f.Name(e => e.IsFixed).Alias(Alias.IsFixed))
                    .Boolean(f => f.Name(e => e.IsHidden).Alias(Alias.IsHidden))
                    .Object<object>(f => f.Name(e => e.Idx).Alias(Alias.IDX).Dynamic())
                    .Object<DataDictionary>(f => f.Name(e => e.Data).Properties(p2 => p2
                        .Text(f2 => f2.Name(Event.KnownDataKeys.Version).Alias(Alias.Version).Analyzer(VERSION_INDEX_ANALYZER).SearchAnalyzer(VERSION_SEARCH_ANALYZER).AddKeywordField().IgnoreAbove(256))
                        .Text(f2 => f2.Name(Event.KnownDataKeys.Level).Alias(Alias.Level).AddKeywordField().IgnoreAbove(256))
                        .Text(f2 => f2.Name(Event.KnownDataKeys.SubmissionMethod).Alias(Alias.SubmissionMethod).AddKeywordField().IgnoreAbove(256))
                            .Object<Location>(f2 => f2.Name(Event.KnownDataKeys.Location).Properties(p3 => p3
                            .Keyword(f3 => f3.Name(r => r.Country).Alias(Alias.LocationCountry))
                            .Keyword(f3 => f3.Name(r => r.Level1).Alias(Alias.LocationLevel1))
                            .Keyword(f3 => f3.Name(r => r.Level2).Alias(Alias.LocationLevel2))
                            .Keyword(f3 => f3.Name(r => r.Locality).Alias(Alias.LocationLocality))))
                        .Object<RequestInfo>(f2 => f2.Name(Event.KnownDataKeys.RequestInfo).Properties(p3 => p3
                            .Text(f3 => f3.Name(r => r.ClientIpAddress).Alias(Alias.IpAddress).IncludeInAll().Analyzer(COMMA_WHITESPACE_ANALYZER).AddKeywordField())
                            .Text(f3 => f3.Name(r => r.UserAgent).Alias(Alias.RequestUserAgent).AddKeywordField())
                            .Text(f3 => f3.Name(r => r.Path).Alias(Alias.RequestPath).IncludeInAll().AddKeywordField())
                            .Object<DataDictionary>(f3 => f3.Name(e => e.Data).Properties(p4 => p4
                                .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.Browser).Alias(Alias.Browser).AddKeywordField(Alias.BrowserRaw))
                                .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserVersion).Alias(Alias.BrowserVersion).AddKeywordField(Alias.BrowserVersionRaw))
                                .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserMajorVersion).Alias(Alias.BrowserMajorVersion))
                                .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.Device).Alias(Alias.Device).AddKeywordField(Alias.DeviceRaw))
                                .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OS).Alias(Alias.OperatingSystem).AddKeywordField(Alias.OperatingSystemRaw))
                                .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OSVersion).Alias(Alias.OperatingSystemVersion).AddKeywordField(Alias.OperatingSystemVersionRaw))
                                .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OSMajorVersion).Alias(Alias.OperatingSystemMajorVersion))
                                .Boolean(f4 => f4.Name(RequestInfo.KnownDataKeys.IsBot).Alias(Alias.RequestIsBot))))))
                        .Object<Error>(f2 => f2.Name(Event.KnownDataKeys.Error).Properties(p3 => p3
                            .Keyword(f3 => f3.Name("all_codes").Alias(Alias.ErrorCode).IncludeInAll().Boost(1.1).AddKeywordField())
                            .Text(f3 => f3.Name("all_messages").Alias(Alias.ErrorMessage).IncludeInAll().AddKeywordField())
                            .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Properties(p4 => p4
                                .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Properties(p5 => p5
                                    .Text(f6 => f6.Name("ExceptionType").Alias(Alias.ErrorTargetType).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2).AddKeywordField()
                                        /*.Fields(fields => fields.Keyword(ss => ss.Name(Alias.ErrorTargetTypeRaw)))*/)
                                    .Text(f6 => f6.Name("Method").Alias(Alias.ErrorTargetMethod).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2).AddKeywordField())))))
                            .Text(f3 => f3.Name("all_types").Alias(Alias.ErrorType).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1).AddKeywordField())))
                        .Object<SimpleError>(f2 => f2.Name(Event.KnownDataKeys.SimpleError).Properties(p3 => p3
                            .Text(f3 => f3.Name("all_messages").Alias(Alias.ErrorMessage).IncludeInAll().AddKeywordField())
                            .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Properties(p4 => p4
                                .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Properties(p5 => p5
                                    .Text(f6 => f6.Name("ExceptionType").Alias(Alias.ErrorTargetType).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2).AddKeywordField())))))
                            .Text(f3 => f3.Name("all_types").Alias(Alias.ErrorType).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1).AddKeywordField())))
                        .Object<EnvironmentInfo>(f2 => f2.Name(Event.KnownDataKeys.EnvironmentInfo).Properties(p3 => p3
                            .Text(f3 => f3.Name(r => r.IpAddress).Alias(Alias.IpAddress).IncludeInAll().Analyzer(COMMA_WHITESPACE_ANALYZER).AddKeywordField())
                            .Text(f3 => f3.Name(r => r.MachineName).Alias(Alias.MachineName).IncludeInAll().Boost(1.1).AddKeywordField())
                            .Text(f3 => f3.Name(r => r.OSName).Alias(Alias.OperatingSystem).AddKeywordField())
                            .Keyword(f3 => f3.Name(r => r.Architecture).Alias(Alias.MachineArchitecture).AddKeywordField())))
                        .Object<UserDescription>(f2 => f2.Name(Event.KnownDataKeys.UserDescription).Properties(p3 => p3
                            .Text(f3 => f3.Name(r => r.Description).Alias(Alias.UserDescription).IncludeInAll().IgnoreAbove(256))
                            .Text(f3 => f3.Name(r => r.EmailAddress).Alias(Alias.UserEmail).Analyzer(EMAIL_ANALYZER).SearchAnalyzer("simple").IncludeInAll().Boost(1.1).AddKeywordField())))
                        .Object<UserInfo>(f2 => f2.Name(Event.KnownDataKeys.UserInfo).Properties(p3 => p3
                            .Text(f3 => f3.Name(r => r.Identity).Alias(Alias.User).Analyzer(EMAIL_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1).AddKeywordField()
                                /*.Fields(fields => fields.Keyword(ss => ss.Name(Alias.UserRaw))) */)
                            .Text(f3 => f3.Name(r => r.Name).Alias(Alias.UserName).IncludeInAll().AddKeywordField())))))
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

        public class Alias {
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
