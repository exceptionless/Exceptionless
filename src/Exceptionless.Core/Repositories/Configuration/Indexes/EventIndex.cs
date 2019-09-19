using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class EventIndex : DailyIndex<PersistentEvent> {
        private const string ALL_WORDS_DELIMITER_TOKEN_FILTER = "all_word_delimiter";
        private const string EDGE_NGRAM_TOKEN_FILTER = "edge_ngram";
        private const string EMAIL_TOKEN_FILTER = "email";
        private const string TYPENAME_TOKEN_FILTER = "typename";
        private const string VERSION_TOKEN_FILTER = "version";
        private const string VERSION_PAD1_TOKEN_FILTER = "version_pad1";
        private const string VERSION_PAD2_TOKEN_FILTER = "version_pad2";
        private const string VERSION_PAD3_TOKEN_FILTER = "version_pad3";
        private const string VERSION_PAD4_TOKEN_FILTER = "version_pad4";
        private const string TLD_STOPWORDS_TOKEN_FILTER = "tld_stopwords";

        //internal const string ALL_ANALYZER = "all";
        internal const string COMMA_WHITESPACE_ANALYZER = "comma_whitespace";
        internal const string EMAIL_ANALYZER = "email";
        internal const string VERSION_INDEX_ANALYZER = "version_index";
        internal const string VERSION_SEARCH_ANALYZER = "version_search";
        internal const string WHITESPACE_LOWERCASE_ANALYZER = "whitespace_lower";
        internal const string TYPENAME_ANALYZER = "typename";
        internal const string STANDARDPLUS_ANALYZER = "standardplus";

        internal const string COMMA_WHITESPACE_TOKENIZER = "comma_whitespace";
        internal const string TYPENAME_HIERARCHY_TOKENIZER = "typename_hierarchy";
        
        internal const string ALL_FIELD = "all";
        
        private readonly ExceptionlessElasticConfiguration _configuration;

        public EventIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "events", 1, doc => ((PersistentEvent)doc).Date.UtcDateTime) {
            _configuration = configuration;
            MaxIndexAge = TimeSpan.FromDays(180);

            AddAlias($"{Name}-today", TimeSpan.FromDays(1));
            AddAlias($"{Name}-last3days", TimeSpan.FromDays(7));
            AddAlias($"{Name}-last7days", TimeSpan.FromDays(7));
            AddAlias($"{Name}-last30days", TimeSpan.FromDays(30));
            AddAlias($"{Name}-last90days", TimeSpan.FromDays(90));
        }

        public override async Task ConfigureAsync() {
            const string FLATTEN_ERRORS_SCRIPT = @"
if (!ctx.containsKey('data') || !(ctx.data.containsKey('@error') || ctx.data.containsKey('@simple_error')))
  return;

def types = [];
def messages = [];
def codes = [];
def err = ctx.data.containsKey('@error') ? ctx.data['@error'] : ctx.data['@simple_error'];
def curr = err;
while (curr != null) {
  if (curr.containsKey('type'))
    types.add(curr.type);
  if (curr.containsKey('message'))
    messages.add(curr.message);
  if (curr.containsKey('code'))
    codes.add(curr.code);
  curr = curr.inner;
}

if (ctx.error == null)
  ctx.error = new HashMap();

ctx.error.type = types;
ctx.error.message = messages;
ctx.error.code = codes;";
            
            const string pipeline = "events-pipeline";
            var response = await Configuration.Client.Ingest.PutPipelineAsync(pipeline, d => d.Processors(p => p
                .Script(s => new ScriptProcessor {
                    Source = FLATTEN_ERRORS_SCRIPT.Replace("\r", String.Empty).Replace("\n", String.Empty).Replace("  ", " ")
                })));

            var logger = Configuration.LoggerFactory.CreateLogger<EventIndex>();
            logger.LogTraceRequest(response);

            if (!response.IsValid) {
                logger.LogError(response.OriginalException, "Error creating the pipeline {Pipeline}: {Message}", pipeline, response.GetErrorMessage());
                throw new ApplicationException($"Error creating the pipeline {pipeline}: {response.GetErrorMessage()}", response.OriginalException);
            }

            await base.ConfigureAsync();
        }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .Analysis(BuildAnalysis)
                .NumberOfShards(_configuration.Options.NumberOfShards)
                .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
                .Setting("index.query.default_field", ALL_FIELD)
                .Setting("index.mapping.total_fields.limit", _configuration.Options.FieldsLimit)
                .Priority(1)));
        }

        public override TypeMappingDescriptor<PersistentEvent> ConfigureIndexMapping(TypeMappingDescriptor<PersistentEvent> map) {
            var mapping = map
                .Dynamic(false)
                .DynamicTemplates(dt => dt.DynamicTemplate("idx_reference", t => t.Match("*-r").Mapping(m => m.Keyword(s => s.IgnoreAbove(256)))))
                .Properties(p => p
                    .SetupDefaults()
                    .Keyword(f => f.Name(e => e.Id).CopyTo(s => s.Field(ALL_FIELD)))
                    .Keyword(f => f.Name(e => e.OrganizationId))
                        .FieldAlias(a => a.Name(Alias.OrganizationId).Path(f => f.OrganizationId))
                    .Keyword(f => f.Name(e => e.ProjectId))
                        .FieldAlias(a => a.Name(Alias.ProjectId).Path(f => f.ProjectId))
                    .Keyword(f => f.Name(e => e.StackId))
                        .FieldAlias(a => a.Name(Alias.StackId).Path(f => f.StackId))
                    .Keyword(f => f.Name(e => e.ReferenceId))
                        .FieldAlias(a => a.Name(Alias.ReferenceId).Path(f => f.ReferenceId))
                    .Keyword(f => f.Name(e => e.Type))
                    .Text(f => f.Name(e => e.Source).CopyTo(s => s.Field(ALL_FIELD)).AddKeywordField())
                    .Date(f => f.Name(e => e.Date))
                    .Text(f => f.Name(e => e.Message).CopyTo(s => s.Field(ALL_FIELD)))
                    .Text(f => f.Name(e => e.Tags).CopyTo(s => s.Field(ALL_FIELD)).Boost(1.2).AddKeywordField())
                        .FieldAlias(a => a.Name(Alias.Tags).Path(f => f.Tags))
                    .GeoPoint(f => f.Name(e => e.Geo))
                    .Scalar(f => f.Value)
                    .Scalar(f => f.Count)
                    .Boolean(f => f.Name(e => e.IsFirstOccurrence))
                        .FieldAlias(a => a.Name(Alias.IsFirstOccurrence).Path(f => f.IsFirstOccurrence))
                    .Boolean(f => f.Name(e => e.IsFixed))
                        .FieldAlias(a => a.Name(Alias.IsFixed).Path(f => f.IsFixed))
                    .Boolean(f => f.Name(e => e.IsHidden))
                        .FieldAlias(a => a.Name(Alias.IsHidden).Path(f => f.IsHidden))
                    .Object<object>(f => f.Name(e => e.Idx).Dynamic())
                    .AddDataDictionaryMappings()
                    .AddCopyToMappings()
                    .AddAliases()
            );

            if (Options != null && Options.EnableMapperSizePlugin)
                return mapping.SizeField(s => s.Enabled());

            return mapping;
        }

        protected override void ConfigureQueryParser(ElasticQueryParserConfiguration config) {
            config
                .SetDefaultFields(new[] { ALL_FIELD })
                .AddQueryVisitor(new EventFieldsQueryVisitor())
                .UseFieldMap(new Dictionary<string, string> {
                    { Alias.BrowserVersion, $"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.BrowserVersion}" },
                    { Alias.BrowserMajorVersion, $"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.BrowserMajorVersion}" },
                    { Alias.User, $"data.{Event.KnownDataKeys.UserInfo}.{nameof(UserInfo.Identity).ToLowerUnderscoredWords()}" },
                    { Alias.UserName, $"data.{Event.KnownDataKeys.UserInfo}.{nameof(UserInfo.Name).ToLowerUnderscoredWords()}" },
                    { Alias.UserEmail, $"data.{Event.KnownDataKeys.UserDescription}.{nameof(UserDescription.EmailAddress).ToLowerUnderscoredWords()}" },
                    { Alias.UserDescription, $"data.{Event.KnownDataKeys.UserDescription}.{nameof(UserDescription.Description).ToLowerUnderscoredWords()}" },
                    { Alias.OperatingSystemVersion, $"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.OSVersion}" },
                    { Alias.OperatingSystemMajorVersion, $"data.{Event.KnownDataKeys.RequestInfo}.data.{nameof(RequestInfo.KnownDataKeys.OSMajorVersion)}" }
                });
        }

        public ElasticsearchOptions Options => (Configuration as ExceptionlessElasticConfiguration)?.Options;

        private AnalysisDescriptor BuildAnalysis(AnalysisDescriptor ad) {
            return ad.Analyzers(a => a
                //.Custom(ALL_ANALYZER, c => c.Filters(ALL_WORDS_DELIMITER_TOKEN_FILTER, EMAIL_TOKEN_FILTER, "lowercase", TLD_STOPWORDS_TOKEN_FILTER, "asciifolding", EDGE_NGRAM_TOKEN_FILTER, "unique").Tokenizer("whitespace"))
                .Pattern(COMMA_WHITESPACE_ANALYZER, p => p.Pattern(@"[,\s]+"))
                .Custom(EMAIL_ANALYZER, c => c.Filters(EMAIL_TOKEN_FILTER, "lowercase", TLD_STOPWORDS_TOKEN_FILTER, EDGE_NGRAM_TOKEN_FILTER, "unique").Tokenizer("keyword"))
                .Custom(VERSION_INDEX_ANALYZER, c => c.Filters(VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, VERSION_TOKEN_FILTER, "lowercase", "unique").Tokenizer("whitespace"))
                .Custom(VERSION_SEARCH_ANALYZER, c => c.Filters(VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, "lowercase").Tokenizer("whitespace"))
                .Custom(WHITESPACE_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("whitespace"))
                .Custom(TYPENAME_ANALYZER, c => c.Filters(TYPENAME_TOKEN_FILTER, "lowercase", "unique").Tokenizer(TYPENAME_HIERARCHY_TOKENIZER))
                .Custom(STANDARDPLUS_ANALYZER, c => c.Filters(TYPENAME_TOKEN_FILTER, "lowercase", "stop", "unique").Tokenizer(COMMA_WHITESPACE_TOKENIZER)))
            .TokenFilters(f => f
                .EdgeNGram(EDGE_NGRAM_TOKEN_FILTER, p => p.MaxGram(50).MinGram(2).Side(EdgeNGramSide.Front))
                .PatternCapture(EMAIL_TOKEN_FILTER, p => p.PreserveOriginal().Patterns("(\\w+)","(\\p{L}+)","(\\d+)","@(.+)","@(.+)\\.","(.+)@"))
                .PatternCapture(TYPENAME_TOKEN_FILTER, p => p.Patterns(@"\.(\w+)", @"([^\()]+)"))
                .PatternCapture(VERSION_TOKEN_FILTER, p => p.Patterns(@"^(\d+)\.", @"^(\d+\.\d+)", @"^(\d+\.\d+\.\d+)"))
                .PatternReplace(VERSION_PAD1_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{1})(?=\.|-|$)").Replacement("$10000$2"))
                .PatternReplace(VERSION_PAD2_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{2})(?=\.|-|$)").Replacement("$1000$2"))
                .PatternReplace(VERSION_PAD3_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{3})(?=\.|-|$)").Replacement("$100$2"))
                .PatternReplace(VERSION_PAD4_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{4})(?=\.|-|$)").Replacement("$10$2"))
                .Stop(TLD_STOPWORDS_TOKEN_FILTER, p => p.StopWords("com", "net", "org", "info", "me", "edu", "mil", "gov", "biz", "co", "io", "dev"))
                .WordDelimiter(ALL_WORDS_DELIMITER_TOKEN_FILTER, p => p.CatenateNumbers().PreserveOriginal().CatenateAll().CatenateWords()))
            .Tokenizers(t => t
                .Pattern(COMMA_WHITESPACE_TOKENIZER, p => p.Pattern(@"[,\s]+"))
                .PathHierarchy(TYPENAME_HIERARCHY_TOKENIZER, p => p.Delimiter('.')));
        }

        public sealed class Alias {
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
            public const string BrowserVersion = "browser.version";
            public const string BrowserMajorVersion = "browser.major";
            public const string RequestIsBot = "bot";

            public const string ClientVersion = "client.version";
            public const string ClientUserAgent = "client.useragent";

            public const string Device = "device";

            public const string OperatingSystem = "os";
            public const string OperatingSystemVersion = "os.version";
            public const string OperatingSystemMajorVersion = "os.major";

            public const string CommandLine = "cmd";
            public const string MachineName = "machine";
            public const string MachineArchitecture = "architecture";

            public const string User = "user";
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
            public const string ErrorTargetMethod = "error.targetmethod";
        }
    }

    internal static class EventIndexExtensions {
        public static PropertiesDescriptor<PersistentEvent> AddAliases(this PropertiesDescriptor<PersistentEvent> descriptor) {
            return descriptor
                    .FieldAlias(a => a.Name(EventIndex.Alias.Version).Path(f => (string)f.Data[Event.KnownDataKeys.Version]))
                    .FieldAlias(a => a.Name(EventIndex.Alias.Level).Path(f => (string)f.Data[Event.KnownDataKeys.Level]))
                    .FieldAlias(a => a.Name(EventIndex.Alias.SubmissionMethod).Path(f => (string)f.Data[Event.KnownDataKeys.SubmissionMethod]))
                    .FieldAlias(a => a.Name(EventIndex.Alias.ClientUserAgent).Path(f => ((SubmissionClient)f.Data[Event.KnownDataKeys.SubmissionClient]).UserAgent))
                    .FieldAlias(a => a.Name(EventIndex.Alias.ClientVersion).Path(f => ((SubmissionClient)f.Data[Event.KnownDataKeys.SubmissionClient]).Version))
                    .FieldAlias(a => a.Name(EventIndex.Alias.LocationCountry).Path(f => ((Location)f.Data[Event.KnownDataKeys.Location]).Country))
                    .FieldAlias(a => a.Name(EventIndex.Alias.LocationLevel1).Path(f => ((Location)f.Data[Event.KnownDataKeys.Location]).Level1))
                    .FieldAlias(a => a.Name(EventIndex.Alias.LocationLevel2).Path(f => ((Location)f.Data[Event.KnownDataKeys.Location]).Level2))
                    .FieldAlias(a => a.Name(EventIndex.Alias.LocationLocality).Path(f => ((Location)f.Data[Event.KnownDataKeys.Location]).Locality))
                    .FieldAlias(a => a.Name(EventIndex.Alias.Browser).Path(f => ((RequestInfo)f.Data[Event.KnownDataKeys.RequestInfo]).Data[RequestInfo.KnownDataKeys.Browser]))
                    .FieldAlias(a => a.Name(EventIndex.Alias.Device).Path(f => ((RequestInfo)f.Data[Event.KnownDataKeys.RequestInfo]).Data[RequestInfo.KnownDataKeys.Device]))
                    .FieldAlias(a => a.Name(EventIndex.Alias.RequestIsBot).Path(f => ((RequestInfo)f.Data[Event.KnownDataKeys.RequestInfo]).Data[RequestInfo.KnownDataKeys.IsBot]))
                    .FieldAlias(a => a.Name(EventIndex.Alias.RequestPath).Path(f => ((RequestInfo)f.Data[Event.KnownDataKeys.RequestInfo]).Path))
                    .FieldAlias(a => a.Name(EventIndex.Alias.RequestUserAgent).Path(f => ((RequestInfo)f.Data[Event.KnownDataKeys.RequestInfo]).UserAgent))
                    .FieldAlias(a => a.Name(EventIndex.Alias.CommandLine).Path(f => ((EnvironmentInfo)f.Data[Event.KnownDataKeys.EnvironmentInfo]).CommandLine))
                    .FieldAlias(a => a.Name(EventIndex.Alias.MachineArchitecture).Path(f => ((EnvironmentInfo)f.Data[Event.KnownDataKeys.EnvironmentInfo]).Architecture))
                    .FieldAlias(a => a.Name(EventIndex.Alias.MachineName).Path(f => ((EnvironmentInfo)f.Data[Event.KnownDataKeys.EnvironmentInfo]).MachineName));
        }
        
        public static PropertiesDescriptor<PersistentEvent> AddCopyToMappings(this PropertiesDescriptor<PersistentEvent> descriptor) {
            return descriptor
                .Text(f => f.Name(EventIndex.ALL_FIELD).Analyzer(EventIndex.STANDARDPLUS_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER))
                .Text(f => f.Name(EventIndex.Alias.IpAddress).Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER))
                .Text(f => f.Name(EventIndex.Alias.OperatingSystem).AddKeywordField())
                .Object<object>(f => f.Name("error").Properties(p1 => p1
                    .Keyword(f3 => f3.Name("code").CopyTo(s => s.Field(EventIndex.ALL_FIELD)).Boost(1.1))
                    .Text(f3 => f3.Name("message").AddKeywordField())
                    .Text(f3 => f3.Name("type").Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).Boost(1.1).CopyTo(s => s.Field(EventIndex.ALL_FIELD)).AddKeywordField())
                    .Text(f6 => f6.Name("targettype").Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).Boost(1.2).CopyTo(s => s.Field(EventIndex.ALL_FIELD)).AddKeywordField())
                    .Text(f6 => f6.Name("targetmethod").Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).Boost(1.2).CopyTo(s => s.Field(EventIndex.ALL_FIELD)).AddKeywordField())));
        }

        public static PropertiesDescriptor<PersistentEvent> AddDataDictionaryMappings(this PropertiesDescriptor<PersistentEvent> descriptor) {
            return descriptor.Object<DataDictionary>(f => f.Name(e => e.Data).Properties(p2 => p2
                .AddVersionMapping()
                .AddLevelMapping()
                .AddSubmissionMethodMapping()
                .AddSubmissionClientMapping()
                .AddLocationMapping()
                .AddRequestInfoMapping()
                .AddErrorMapping()
                .AddSimpleErrorMapping()
                .AddEnvironmentInfoMapping()
                .AddUserDescriptionMapping()
                .AddUserInfoMapping()));
        }

        private static PropertiesDescriptor<DataDictionary> AddVersionMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.Version).Analyzer(EventIndex.VERSION_INDEX_ANALYZER).SearchAnalyzer(EventIndex.VERSION_SEARCH_ANALYZER).AddKeywordField());
        }

        private static PropertiesDescriptor<DataDictionary> AddLevelMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.Level).AddKeywordField());
        }

        private static PropertiesDescriptor<DataDictionary> AddSubmissionMethodMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.SubmissionMethod).AddKeywordField());
        }

        private static PropertiesDescriptor<DataDictionary> AddSubmissionClientMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<SubmissionClient>(f2 => f2.Name(Event.KnownDataKeys.SubmissionClient).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.IpAddress).CopyTo(fd => fd.Fields(EventIndex.ALL_FIELD, EventIndex.Alias.IpAddress)).Index(false))
                .Text(f3 => f3.Name(r => r.UserAgent).AddKeywordField())
                .Text(f3 => f3.Name(r => r.Version).AddKeywordField())));
        }

        private static PropertiesDescriptor<DataDictionary> AddLocationMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<Location>(f2 => f2.Name(Event.KnownDataKeys.Location).Properties(p3 => p3
                .Keyword(f3 => f3.Name(r => r.Country))
                .Keyword(f3 => f3.Name(r => r.Level1))
                .Keyword(f3 => f3.Name(r => r.Level2))
                .Keyword(f3 => f3.Name(r => r.Locality))));
        }

        private static PropertiesDescriptor<DataDictionary> AddRequestInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<RequestInfo>(f2 => f2.Name(Event.KnownDataKeys.RequestInfo).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.ClientIpAddress).CopyTo(fd => fd.Fields(EventIndex.ALL_FIELD, EventIndex.Alias.IpAddress)).Index(false))
                .Text(f3 => f3.Name(r => r.UserAgent).AddKeywordField())
                .Text(f3 => f3.Name(r => r.Path).CopyTo(s => s.Field(EventIndex.ALL_FIELD)).AddKeywordField())
                .Object<DataDictionary>(f3 => f3.Name(e => e.Data).Properties(p4 => p4
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.Browser).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserVersion).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserMajorVersion).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.Device).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OS).CopyTo(fd => fd.Field(EventIndex.Alias.OperatingSystem)).Index(false))
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OSVersion).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OSMajorVersion))
                    .Boolean(f4 => f4.Name(RequestInfo.KnownDataKeys.IsBot))))));
        }

        private static PropertiesDescriptor<DataDictionary> AddErrorMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<Error>(f2 => f2.Name(Event.KnownDataKeys.Error).Properties(p3 => p3
                .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Properties(p4 => p4
                    .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Properties(p5 => p5
                        .Text(f6 => f6.Name("ExceptionType").CopyTo(fd => fd.Fields(EventIndex.ALL_FIELD, EventIndex.Alias.ErrorTargetType)).Index(false))
                        .Text(f6 => f6.Name("Method").CopyTo(fd => fd.Fields(EventIndex.ALL_FIELD, EventIndex.Alias.ErrorTargetMethod)).Index(false))))))));
        }

        private static PropertiesDescriptor<DataDictionary> AddSimpleErrorMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<SimpleError>(f2 => f2.Name(Event.KnownDataKeys.SimpleError).Properties(p3 => p3
                .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Properties(p4 => p4
                    .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Properties(p5 => p5
                        .Text(f6 => f6.Name("ExceptionType").CopyTo(fd => fd.Fields(EventIndex.ALL_FIELD, EventIndex.Alias.ErrorTargetType)).Index(false))))))));
        }

        private static PropertiesDescriptor<DataDictionary> AddEnvironmentInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<EnvironmentInfo>(f2 => f2.Name(Event.KnownDataKeys.EnvironmentInfo).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.IpAddress).CopyTo(fd => fd.Fields(EventIndex.ALL_FIELD, EventIndex.Alias.IpAddress)).Index(false))
                .Text(f3 => f3.Name(r => r.MachineName).CopyTo(s => s.Field(EventIndex.ALL_FIELD)).Boost(1.1).AddKeywordField())
                .Text(f3 => f3.Name(r => r.OSName).CopyTo(fd => fd.Field(EventIndex.Alias.OperatingSystem)))
                .Text(f3 => f3.Name(r => r.CommandLine))
                .Keyword(f3 => f3.Name(r => r.Architecture))));
        }

        private static PropertiesDescriptor<DataDictionary> AddUserDescriptionMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<UserDescription>(f2 => f2.Name(Event.KnownDataKeys.UserDescription).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.Description).CopyTo(s => s.Field(EventIndex.ALL_FIELD)))
                .Text(f3 => f3.Name(r => r.EmailAddress).Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer("simple").CopyTo(s => s.Field(EventIndex.ALL_FIELD)).Boost(1.1).AddKeywordField())));
        }

        private static PropertiesDescriptor<DataDictionary> AddUserInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<UserInfo>(f2 => f2.Name(Event.KnownDataKeys.UserInfo).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.Identity).Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).CopyTo(s => s.Field(EventIndex.ALL_FIELD)).Boost(1.1).AddKeywordField())
                .Text(f3 => f3.Name(r => r.Name).CopyTo(s => s.Field(EventIndex.ALL_FIELD)).AddKeywordField())));
        }
    }
}
