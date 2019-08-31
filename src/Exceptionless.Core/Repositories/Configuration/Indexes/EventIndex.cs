using System;
using System.Threading.Tasks;
using Exceptionless.Core.Configuration;
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
        private const string EMAIL_TOKEN_FILTER = "email";
        private const string TYPENAME_TOKEN_FILTER = "typename";
        private const string VERSION_TOKEN_FILTER = "version";
        private const string VERSION_PAD1_TOKEN_FILTER = "version_pad1";
        private const string VERSION_PAD2_TOKEN_FILTER = "version_pad2";
        private const string VERSION_PAD3_TOKEN_FILTER = "version_pad3";
        private const string VERSION_PAD4_TOKEN_FILTER = "version_pad4";

        internal const string COMMA_WHITESPACE_ANALYZER = "comma_whitespace";
        internal const string EMAIL_ANALYZER = "email";
        internal const string VERSION_INDEX_ANALYZER = "version_index";
        internal const string VERSION_SEARCH_ANALYZER = "version_search";
        internal const string WHITESPACE_LOWERCASE_ANALYZER = "whitespace_lower";
        internal const string TYPENAME_ANALYZER = "typename";
        internal const string STANDARDPLUS_ANALYZER = "standardplus";

        internal const string COMMA_WHITESPACE_TOKENIZER = "comma_whitespace";
        internal const string TYPENAME_HIERARCHY_TOKENIZER = "typename_hierarchy";
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
  return null;

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
            var pipeline = "events-pipeline";

            var response = await Configuration.Client.Ingest.PutPipelineAsync(pipeline, d => d.Processors(p => p
                .Script(s => new ScriptProcessor { Source = FLATTEN_ERRORS_SCRIPT.Replace("\r", String.Empty).Replace("\n", String.Empty).Replace("  ", " ") })));

            var logger = Configuration.LoggerFactory.CreateLogger<EventIndex>();
            logger.LogTrace(response.GetRequest());

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
                .Setting("index.mapping.total_fields.limit", _configuration.Options.FieldsLimit)
                .Priority(1)));
        }

        public override TypeMappingDescriptor<PersistentEvent> ConfigureIndexMapping(TypeMappingDescriptor<PersistentEvent> map) {
            var mapping = map
                .Dynamic(false)
                .DynamicTemplates(dt => dt.DynamicTemplate("idx_reference", t => t.Match("*-r").Mapping(m => m.Keyword(s => s.IgnoreAbove(256)))))
                .AllField(a => a.Analyzer(EventIndex.STANDARDPLUS_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER))
                .Properties(p => p
                    .SetupDefaults()
                    .Text(f => f.Name("_all"))
                    .Keyword(f => f.Name(e => e.Id).IncludeInAll())
                        .FieldAlias(a => a.Name(Alias.Id).Path(f => f.Id))
                    .Keyword(f => f.Name(e => e.OrganizationId))
                        .FieldAlias(a => a.Name(Alias.OrganizationId).Path(f => f.OrganizationId))
                    .Keyword(f => f.Name(e => e.ProjectId))
                        .FieldAlias(a => a.Name(Alias.ProjectId).Path(f => f.ProjectId))
                    .Keyword(f => f.Name(e => e.StackId))
                        .FieldAlias(a => a.Name(Alias.StackId).Path(f => f.StackId))
                    .Keyword(f => f.Name(e => e.ReferenceId))
                        .FieldAlias(a => a.Name(Alias.ReferenceId).Path(f => f.ReferenceId))
                    .Keyword(f => f.Name(e => e.Type))
                        .FieldAlias(a => a.Name(Alias.Type).Path(f => f.Type))
                    .Text(f => f.Name(e => e.Source).IncludeInAll().AddKeywordField())
                        .FieldAlias(a => a.Name(Alias.Source).Path(f => f.Source))
                    .Date(f => f.Name(e => e.Date))
                        .FieldAlias(a => a.Name(Alias.Date).Path(f => f.Date))
                    .Text(f => f.Name(e => e.Message).IncludeInAll())
                        .FieldAlias(a => a.Name(Alias.Message).Path(f => f.Message))
                    .Text(f => f.Name(e => e.Tags).IncludeInAll().Boost(1.2).AddKeywordField())
                        .FieldAlias(a => a.Name(Alias.Tags).Path(f => f.Tags))
                    .GeoPoint(f => f.Name(e => e.Geo))
                        .FieldAlias(a => a.Name(Alias.Geo).Path(f => f.Geo))
                    .Scalar(f => f.Value)
                        .FieldAlias(a => a.Name(Alias.Value).Path(f => f.Value))
                    .Scalar(f => f.Count)
                        .FieldAlias(a => a.Name(Alias.Count).Path(f => f.Count))
                    .Boolean(f => f.Name(e => e.IsFirstOccurrence))
                        .FieldAlias(a => a.Name(Alias.IsFirstOccurrence).Path(f => f.IsFirstOccurrence))
                    .Boolean(f => f.Name(e => e.IsFixed))
                        .FieldAlias(a => a.Name(Alias.IsFixed).Path(f => f.IsFixed))
                    .Boolean(f => f.Name(e => e.IsHidden))
                        .FieldAlias(a => a.Name(Alias.IsHidden).Path(f => f.IsHidden))
                    .Object<object>(f => f.Name(e => e.Idx).Dynamic())
                        .FieldAlias(a => a.Name(Alias.IDX).Path(f => f.Idx))
                    .AddDataDictionaryMappings()
                    .AddCopyToMappings()
            );

            if (Options != null && Options.EnableMapperSizePlugin)
                return mapping.SizeField(s => s.Enabled());

            return mapping;
        }

        protected override void ConfigureQueryParser(ElasticQueryParserConfiguration config) {
            config.AddQueryVisitor(new EventFieldsQueryVisitor());
        }

        public ElasticsearchOptions Options => (Configuration as ExceptionlessElasticConfiguration)?.Options;

        private AnalysisDescriptor BuildAnalysis(AnalysisDescriptor ad) {
            return ad.Analyzers(a => a
                .Pattern(COMMA_WHITESPACE_ANALYZER, p => p.Pattern(@"[,\s]+"))
                .Custom(EMAIL_ANALYZER, c => c.Filters(EMAIL_TOKEN_FILTER, "lowercase", "unique").Tokenizer("keyword"))
                .Custom(VERSION_INDEX_ANALYZER, c => c.Filters(VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, VERSION_TOKEN_FILTER, "lowercase", "unique").Tokenizer("whitespace"))
                .Custom(VERSION_SEARCH_ANALYZER, c => c.Filters(VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, "lowercase").Tokenizer("whitespace"))
                .Custom(WHITESPACE_LOWERCASE_ANALYZER, c => c.Filters("lowercase").Tokenizer("whitespace"))
                .Custom(TYPENAME_ANALYZER, c => c.Filters(TYPENAME_TOKEN_FILTER, "lowercase", "unique").Tokenizer(TYPENAME_HIERARCHY_TOKENIZER))
                .Custom(STANDARDPLUS_ANALYZER, c => c.Filters("standard", TYPENAME_TOKEN_FILTER, "lowercase", "stop", "unique").Tokenizer(COMMA_WHITESPACE_TOKENIZER)))
            .TokenFilters(f => f
                .PatternCapture(EMAIL_TOKEN_FILTER, p => p.Patterns(@"(\w+)", @"(\p{L}+)", @"(\d+)", "(.+)@", "@(.+)"))
                .PatternCapture(TYPENAME_TOKEN_FILTER, p => p.Patterns(@"\.(\w+)", @"([^\()]+)"))
                .PatternCapture(VERSION_TOKEN_FILTER, p => p.Patterns(@"^(\d+)\.", @"^(\d+\.\d+)", @"^(\d+\.\d+\.\d+)"))
                .PatternReplace(VERSION_PAD1_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{1})(?=\.|-|$)").Replacement("$10000$2"))
                .PatternReplace(VERSION_PAD2_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{2})(?=\.|-|$)").Replacement("$1000$2"))
                .PatternReplace(VERSION_PAD3_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{3})(?=\.|-|$)").Replacement("$100$2"))
                .PatternReplace(VERSION_PAD4_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{4})(?=\.|-|$)").Replacement("$10$2")))
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
        public static PropertiesDescriptor<PersistentEvent> AddCopyToMappings(this PropertiesDescriptor<PersistentEvent> descriptor) {
            return descriptor
                .Text(f => f.Name(EventIndex.Alias.IpAddress).Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER))
                .Text(f => f.Name(EventIndex.Alias.OperatingSystem).AddKeywordField())
                .Object<object>(f => f.Name("error").IncludeInAll().Properties(p1 => p1
                    .Keyword(f3 => f3.Name("code").Boost(1.1))
                    .Text(f3 => f3.Name("message").AddKeywordField())
                    .Text(f3 => f3.Name("type").Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).Boost(1.1).AddKeywordField())
                    .Text(f6 => f6.Name("targettype").Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).Boost(1.2).AddKeywordField())
                    .Text(f6 => f6.Name("targetmethod").Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).Boost(1.2).AddKeywordField())));
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
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.Version).Analyzer(EventIndex.VERSION_INDEX_ANALYZER).SearchAnalyzer(EventIndex.VERSION_SEARCH_ANALYZER).AddKeywordField())
                .FieldAlias(a => a.Name(EventIndex.Alias.Version).Path(Event.KnownDataKeys.Version));
        }

        private static PropertiesDescriptor<DataDictionary> AddLevelMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.Level).AddKeywordField())
                .FieldAlias(a => a.Name(EventIndex.Alias.Level).Path(Event.KnownDataKeys.Level));
        }

        private static PropertiesDescriptor<DataDictionary> AddSubmissionMethodMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.SubmissionMethod).RootAlias(EventIndex.Alias.SubmissionMethod).AddKeywordField());
        }

        private static PropertiesDescriptor<DataDictionary> AddSubmissionClientMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<SubmissionClient>(f2 => f2.Name(Event.KnownDataKeys.SubmissionClient).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.IpAddress).CopyTo(fd => fd.Field(EventIndex.Alias.IpAddress)).Index(false).IncludeInAll())
                .Text(f3 => f3.Name(r => r.UserAgent).RootAlias(EventIndex.Alias.ClientUserAgent).AddKeywordField())
                .Text(f3 => f3.Name(r => r.Version).RootAlias(EventIndex.Alias.ClientVersion).AddKeywordField())));
        }

        private static PropertiesDescriptor<DataDictionary> AddLocationMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<Location>(f2 => f2.Name(Event.KnownDataKeys.Location).Properties(p3 => p3
                .Keyword(f3 => f3.Name(r => r.Country).RootAlias(EventIndex.Alias.LocationCountry))
                .Keyword(f3 => f3.Name(r => r.Level1).RootAlias(EventIndex.Alias.LocationLevel1))
                .Keyword(f3 => f3.Name(r => r.Level2).RootAlias(EventIndex.Alias.LocationLevel2))
                .Keyword(f3 => f3.Name(r => r.Locality).RootAlias(EventIndex.Alias.LocationLocality))));
        }

        private static PropertiesDescriptor<DataDictionary> AddRequestInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<RequestInfo>(f2 => f2.Name(Event.KnownDataKeys.RequestInfo).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.ClientIpAddress).CopyTo(fd => fd.Field(EventIndex.Alias.IpAddress)).Index(false).IncludeInAll())
                .Text(f3 => f3.Name(r => r.UserAgent).RootAlias(EventIndex.Alias.RequestUserAgent).AddKeywordField())
                .Text(f3 => f3.Name(r => r.Path).RootAlias(EventIndex.Alias.RequestPath).IncludeInAll().AddKeywordField())
                .Object<DataDictionary>(f3 => f3.Name(e => e.Data).Properties(p4 => p4
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.Browser).RootAlias(EventIndex.Alias.Browser).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserVersion).RootAlias(EventIndex.Alias.BrowserVersion).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserMajorVersion).RootAlias(EventIndex.Alias.BrowserMajorVersion).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.Device).RootAlias(EventIndex.Alias.Device).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OS).CopyTo(fd => fd.Field(EventIndex.Alias.OperatingSystem)).Index(false))
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OSVersion).RootAlias(EventIndex.Alias.OperatingSystemVersion).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OSMajorVersion).RootAlias(EventIndex.Alias.OperatingSystemMajorVersion))
                    .Boolean(f4 => f4.Name(RequestInfo.KnownDataKeys.IsBot).RootAlias(EventIndex.Alias.RequestIsBot))))));
        }

        private static PropertiesDescriptor<DataDictionary> AddErrorMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<Error>(f2 => f2.Name(Event.KnownDataKeys.Error).Properties(p3 => p3
                .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Properties(p4 => p4
                    .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Properties(p5 => p5
                        .Text(f6 => f6.Name("ExceptionType").CopyTo(fd => fd.Field(EventIndex.Alias.ErrorTargetType)).Index(false).IncludeInAll())
                        .Text(f6 => f6.Name("Method").CopyTo(fd => fd.Field(EventIndex.Alias.ErrorTargetMethod)).Index(false).IncludeInAll())))))));
        }

        private static PropertiesDescriptor<DataDictionary> AddSimpleErrorMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<SimpleError>(f2 => f2.Name(Event.KnownDataKeys.SimpleError).Properties(p3 => p3
                .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Properties(p4 => p4
                    .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Properties(p5 => p5
                        .Text(f6 => f6.Name("ExceptionType").CopyTo(fd => fd.Field(EventIndex.Alias.ErrorTargetType)).Index(false).IncludeInAll())))))));
        }

        private static PropertiesDescriptor<DataDictionary> AddEnvironmentInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<EnvironmentInfo>(f2 => f2.Name(Event.KnownDataKeys.EnvironmentInfo).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.IpAddress).CopyTo(fd => fd.Field(EventIndex.Alias.IpAddress)).Index(false).IncludeInAll())
                .Text(f3 => f3.Name(r => r.MachineName).RootAlias(EventIndex.Alias.MachineName).IncludeInAll().Boost(1.1).AddKeywordField())
                .Text(f3 => f3.Name(r => r.OSName).CopyTo(fd => fd.Field(EventIndex.Alias.OperatingSystem)))
                .Text(f3 => f3.Name(r => r.CommandLine).RootAlias(EventIndex.Alias.CommandLine))
                .Keyword(f3 => f3.Name(r => r.Architecture).RootAlias(EventIndex.Alias.MachineArchitecture))));
        }

        private static PropertiesDescriptor<DataDictionary> AddUserDescriptionMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<UserDescription>(f2 => f2.Name(Event.KnownDataKeys.UserDescription).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.Description).RootAlias(EventIndex.Alias.UserDescription).IncludeInAll())
                .Text(f3 => f3.Name(r => r.EmailAddress).RootAlias(EventIndex.Alias.UserEmail).Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer("simple").IncludeInAll().Boost(1.1).AddKeywordField())));
        }

        private static PropertiesDescriptor<DataDictionary> AddUserInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<UserInfo>(f2 => f2.Name(Event.KnownDataKeys.UserInfo).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.Identity).RootAlias(EventIndex.Alias.User).Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1).AddKeywordField())
                .Text(f3 => f3.Name(r => r.Name).RootAlias(EventIndex.Alias.UserName).IncludeInAll().AddKeywordField())));
        }
    }
}
