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
                        .AllField(a => a.Enabled(false).Analyzer(STANDARDPLUS_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER))
                        .DisableSizeField(false) // Change to Size Field
                        .Transform(t => t.Add(a => a.Script(FLATTEN_ERRORS_SCRIPT).Language(ScriptLang.Groovy)))
                        .Properties(p => p
                            .Date(f => f.Name(e => e.CreatedUtc).IndexName(Fields.PersistentEvent.CreatedUtc))
                            .String(f => f.Name(e => e.Id).IndexName(Fields.PersistentEvent.Id).Index(FieldIndexOption.NotAnalyzed).IncludeInAll())
                            .String(f => f.Name(e => e.OrganizationId).IndexName(Fields.PersistentEvent.OrganizationId).Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.ProjectId).IndexName(Fields.PersistentEvent.ProjectId).Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.StackId).IndexName(Fields.PersistentEvent.StackId).Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.ReferenceId).IndexName(Fields.PersistentEvent.ReferenceId).Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.Type).IndexName(Fields.PersistentEvent.Type).Index(FieldIndexOption.NotAnalyzed))
                            .String(f => f.Name(e => e.Source).IndexName(Fields.PersistentEvent.Source).Index(FieldIndexOption.Analyzed).IncludeInAll())
                            .Date(f => f.Name(e => e.Date).IndexName(Fields.PersistentEvent.Date))
                            .String(f => f.Name(e => e.Message).IndexName(Fields.PersistentEvent.Message).Index(FieldIndexOption.Analyzed).IncludeInAll())
                            .String(f => f.Name(e => e.Tags).IndexName(Fields.PersistentEvent.Tags).Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.2))
                            .GeoPoint(f => f.Name(e => e.Geo).IndexName(Fields.PersistentEvent.Value).LatLon())
                            .Number(f => f.Name(e => e.Value).IndexName(Fields.PersistentEvent.Value))
                            .Boolean(f => f.Name(e => e.IsFirstOccurrence).IndexName(Fields.PersistentEvent.IsFirstOccurrence))
                            .Boolean(f => f.Name(e => e.IsFixed).IndexName(Fields.PersistentEvent.IsFixed))
                            .Boolean(f => f.Name(e => e.IsHidden).IndexName(Fields.PersistentEvent.IsHidden))
                            .Object<object>(f => f.Name(Fields.PersistentEvent.IDX).Dynamic())
                            .Object<DataDictionary>(f => f.Name(e => e.Data).Path("just_name").Properties(p2 => p2
                                .String(f2 => f2.Name(Event.KnownDataKeys.Version).IndexName(Fields.PersistentEvent.Version).Index(FieldIndexOption.Analyzed).Analyzer(VERSION_INDEX_ANALYZER).SearchAnalyzer(VERSION_SEARCH_ANALYZER))
                                .String(f2 => f2.Name(Event.KnownDataKeys.Level).IndexName(Fields.PersistentEvent.Level).Index(FieldIndexOption.Analyzed))
                                .String(f2 => f2.Name(Event.KnownDataKeys.SubmissionMethod).IndexName(Fields.PersistentEvent.SubmissionMethod).Index(FieldIndexOption.Analyzed))
                                 .Object<Location>(f2 => f2.Name(Event.KnownDataKeys.Location).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.Country).IndexName(Fields.PersistentEvent.LocationCountry).Index(FieldIndexOption.NotAnalyzed))
                                    .String(f3 => f3.Name(r => r.Country).IndexName(Fields.PersistentEvent.LocationLevel1).Index(FieldIndexOption.NotAnalyzed))
                                    .String(f3 => f3.Name(r => r.Country).IndexName(Fields.PersistentEvent.LocationLevel2).Index(FieldIndexOption.NotAnalyzed))
                                    .String(f3 => f3.Name(r => r.Country).IndexName(Fields.PersistentEvent.LocationLocality).Index(FieldIndexOption.NotAnalyzed))))
                                .Object<RequestInfo>(f2 => f2.Name(Event.KnownDataKeys.RequestInfo).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.ClientIpAddress).IndexName(Fields.PersistentEvent.IpAddress).Index(FieldIndexOption.Analyzed).IncludeInAll().Analyzer(COMMA_WHITESPACE_ANALYZER))
                                    .String(f3 => f3.Name(r => r.UserAgent).IndexName(Fields.PersistentEvent.RequestUserAgent).Index(FieldIndexOption.Analyzed))
                                    .String(f3 => f3.Name(r => r.Path).IndexName(Fields.PersistentEvent.RequestPath).Index(FieldIndexOption.Analyzed).IncludeInAll())
                                    .Object<DataDictionary>(f3 => f3.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.Browser).IndexName(Fields.PersistentEvent.Browser).Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name(Fields.PersistentEvent.BrowserRaw).Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserVersion).IndexName(Fields.PersistentEvent.BrowserVersion).Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name(Fields.PersistentEvent.BrowserVersionRaw).Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserMajorVersion).IndexName(Fields.PersistentEvent.BrowserMajorVersion).Index(FieldIndexOption.NotAnalyzed))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.Device).IndexName(Fields.PersistentEvent.Device).Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name(Fields.PersistentEvent.DeviceRaw).Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OS).IndexName(Fields.PersistentEvent.OperatingSystem).Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name(Fields.PersistentEvent.OperatingSystemRaw).Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OSVersion).IndexName(Fields.PersistentEvent.OperatingSystemVersion).Index(FieldIndexOption.Analyzed)
                                            .Fields(fields => fields.String(ss => ss.Name(Fields.PersistentEvent.OperatingSystemVersionRaw).Index(FieldIndexOption.NotAnalyzed))))
                                        .String(f4 => f4.Name(RequestInfo.KnownDataKeys.OSMajorVersion).IndexName(Fields.PersistentEvent.OperatingSystemMajorVersion).Index(FieldIndexOption.NotAnalyzed))
                                        .Boolean(f4 => f4.Name(RequestInfo.KnownDataKeys.IsBot).IndexName(Fields.PersistentEvent.RequestIsBot))))))
                                .Object<Error>(f2 => f2.Name(Event.KnownDataKeys.Error).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name("all_codes").IndexName(Fields.PersistentEvent.ErrorCode).Index(FieldIndexOption.NotAnalyzed).Analyzer("whitespace").IncludeInAll().Boost(1.1))
                                    .String(f3 => f3.Name("all_messages").IndexName(Fields.PersistentEvent.ErrorMessage).Index(FieldIndexOption.Analyzed).IncludeInAll())
                                    .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                        .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Path("just_name").Properties(p5 => p5
                                            .String(f6 => f6.Name("ExceptionType").IndexName(Fields.PersistentEvent.ErrorTargetType).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2)
                                                .Fields(fields => fields.String(ss => ss.Name(Fields.PersistentEvent.ErrorTargetTypeRaw).Index(FieldIndexOption.NotAnalyzed))))
                                            .String(f6 => f6.Name("Method").IndexName(Fields.PersistentEvent.ErrorTargetMethod).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2))))))
                                    .String(f3 => f3.Name("all_types").IndexName(Fields.PersistentEvent.ErrorType).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1))))
                                .Object<SimpleError>(f2 => f2.Name(Event.KnownDataKeys.SimpleError).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name("all_messages").IndexName(Fields.PersistentEvent.ErrorMessage).Index(FieldIndexOption.Analyzed).IncludeInAll())
                                    .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Path("just_name").Properties(p4 => p4
                                        .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Path("just_name").Properties(p5 => p5
                                            .String(f6 => f6.Name("ExceptionType").IndexName(Fields.PersistentEvent.ErrorTargetType).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2))))))
                                    .String(f3 => f3.Name("all_types").IndexName(Fields.PersistentEvent.ErrorType).Index(FieldIndexOption.Analyzed).Analyzer(TYPENAME_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1))))
                                .Object<EnvironmentInfo>(f2 => f2.Name(Event.KnownDataKeys.EnvironmentInfo).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.IpAddress).IndexName(Fields.PersistentEvent.IpAddress).Index(FieldIndexOption.Analyzed).IncludeInAll().Analyzer(COMMA_WHITESPACE_ANALYZER))
                                    .String(f3 => f3.Name(r => r.MachineName).IndexName(Fields.PersistentEvent.MachineName).Index(FieldIndexOption.Analyzed).IncludeInAll().Boost(1.1))
                                    .String(f3 => f3.Name(r => r.OSName).IndexName(Fields.PersistentEvent.OperatingSystem).Index(FieldIndexOption.Analyzed))
                                    .String(f3 => f3.Name(r => r.Architecture).IndexName(Fields.PersistentEvent.MachineArchitecture).Index(FieldIndexOption.NotAnalyzed))))
                                .Object<UserDescription>(f2 => f2.Name(Event.KnownDataKeys.UserDescription).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.Description).IndexName(Fields.PersistentEvent.UserDescription).Index(FieldIndexOption.Analyzed).IncludeInAll())
                                    .String(f3 => f3.Name(r => r.EmailAddress).IndexName(Fields.PersistentEvent.UserEmail).Index(FieldIndexOption.Analyzed).Analyzer(EMAIL_ANALYZER).SearchAnalyzer("simple").IncludeInAll().Boost(1.1))))
                                .Object<UserInfo>(f2 => f2.Name(Event.KnownDataKeys.UserInfo).Path("just_name").Properties(p3 => p3
                                    .String(f3 => f3.Name(r => r.Identity).IndexName(Fields.PersistentEvent.User).Index(FieldIndexOption.Analyzed).Analyzer(EMAIL_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1)
                                        .Fields(fields => fields.String(ss => ss.Name(Fields.PersistentEvent.UserRaw).Index(FieldIndexOption.NotAnalyzed))))
                                    .String(f3 => f3.Name(r => r.Name).IndexName(Fields.PersistentEvent.UserName).Index(FieldIndexOption.Analyzed).IncludeInAll())))))
                        )));
        }
        
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
        private const string VERSION_PAD4_TOKEN_FILTER = "version_pad4";

        private IAnalysis BuildAnalysisSettings() {
            return new Analysis {
                Analyzers = new Analyzers {
                    { COMMA_WHITESPACE_ANALYZER, new PatternAnalyzer { Pattern = @"[,\s]+" } },
                    { EMAIL_ANALYZER, new CustomAnalyzer { Tokenizer = "keyword", Filter = new [] { EMAIL_TOKEN_FILTER, "lowercase", "unique" } } },
                    { VERSION_INDEX_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, VERSION_TOKEN_FILTER, "lowercase", "unique" } } },
                    { VERSION_SEARCH_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, "lowercase" } } },
                    { WHITESPACE_LOWERCASE_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { "lowercase" } } },
                    { TYPENAME_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { TYPENAME_TOKEN_FILTER, "lowercase", "unique" } } },
                    { STANDARDPLUS_ANALYZER, new CustomAnalyzer { Tokenizer = "whitespace", Filter = new [] { "standard", TYPENAME_TOKEN_FILTER, "lowercase", "stop", "unique" } } }
                },
                TokenFilters = new TokenFilters {
                    { EMAIL_TOKEN_FILTER, new PatternCaptureTokenFilter { Patterns = new[] { @"(\w+)", @"(\p{L}+)", @"(\d+)", @"(.+)@", @"@(.+)" } } },
                    { TYPENAME_TOKEN_FILTER, new PatternCaptureTokenFilter { Patterns = new[] { @"\.(\w+)" } } },
                    { VERSION_TOKEN_FILTER, new PatternCaptureTokenFilter { Patterns = new[] { @"^(\d+)\.", @"^(\d+\.\d+)", @"^(\d+\.\d+\.\d+)" } } },
                    { VERSION_PAD1_TOKEN_FILTER, new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{1})(?=\.|$)", Replacement = @"$10000$2" } },
                    { VERSION_PAD2_TOKEN_FILTER, new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{2})(?=\.|$)", Replacement = @"$1000$2" } },
                    { VERSION_PAD3_TOKEN_FILTER, new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{3})(?=\.|$)", Replacement = @"$100$2" } },
                    { VERSION_PAD4_TOKEN_FILTER, new PatternReplaceTokenFilter { Pattern = @"(\.|^)(\d{4})(?=\.|$)", Replacement = @"$10$2" } }
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
                public const string BrowserRaw = "browser_raw";
                public const string BrowserVersion = "browser_version";
                public const string BrowserVersionRaw = "browser_version_raw";
                public const string BrowserMajorVersion = "browser_major";
                public const string RequestIsBot = "bot";
                
                public const string Device = "device";
                public const string DeviceRaw = "device_raw";

                public const string OperatingSystem = "os";
                public const string OperatingSystemRaw = "os_raw";
                public const string OperatingSystemVersion = "os_version";
                public const string OperatingSystemVersionRaw = "os_version_raw";
                public const string OperatingSystemMajorVersion = "os_major";

                public const string MachineName = "machine";
                public const string MachineArchitecture = "architecture";

                public const string User = "user";
                public const string UserRaw = "user_raw";
                public const string UserName = "user_name";
                public const string UserEmail = "user_email";
                public const string UserDescription = "user_description";
                
                public const string LocationCountry = "country";
                public const string LocationLevel1 = "level1";
                public const string LocationLevel2 = "level2";
                public const string LocationLocality = "locality";
                
                public const string ErrorCode = "error_code";
                public const string ErrorType = "error_type";
                public const string ErrorMessage = "error_message";
                public const string ErrorTargetType = "error_targettype";
                public const string ErrorTargetTypeRaw = "error_targettype_raw";
                public const string ErrorTargetMethod = "error_targetmethod";
            }
        }
    }
}
