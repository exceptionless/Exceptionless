using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class EventIndex : DailyIndex<PersistentEvent>
{
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public EventIndex(ExceptionlessElasticConfiguration configuration, IServiceProvider serviceProvider, AppOptions appOptions) : base(configuration, configuration.Options.ScopePrefix + "events", 1, doc => ((PersistentEvent)doc).Date.UtcDateTime)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;

        if (appOptions.MaximumRetentionDays > 0)
            MaxIndexAge = TimeSpan.FromDays(appOptions.MaximumRetentionDays);

        AddAlias($"{Name}-today", TimeSpan.FromDays(1));
        AddAlias($"{Name}-last3days", TimeSpan.FromDays(7));
        AddAlias($"{Name}-last7days", TimeSpan.FromDays(7));
        AddAlias($"{Name}-last30days", TimeSpan.FromDays(30));
        AddAlias($"{Name}-last90days", TimeSpan.FromDays(90));
    }

    protected override void ConfigureQueryBuilder(ElasticQueryBuilder builder)
    {
        var stacksRepository = _serviceProvider.GetRequiredService<IStackRepository>();
        var cacheClient = _serviceProvider.GetRequiredService<ICacheClient>();
        base.ConfigureQueryBuilder(builder);
        builder.RegisterBefore<ParsedExpressionQueryBuilder>(new EventStackFilterQueryBuilder(stacksRepository, cacheClient, _configuration.LoggerFactory));
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<PersistentEvent> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .DynamicTemplates(dt => dt
                .Add("idx_bool", t => t.Match("*-b").Mapping(m => m.Boolean(s => { })))
                .Add("idx_date", t => t.Match("*-d").Mapping(m => m.Date(s => { })))
                .Add("idx_number", t => t.Match("*-n").Mapping(m => m.DoubleNumber(s => { })))
                .Add("idx_reference", t => t.Match("*-r").Mapping(m => m.Keyword(s => s.IgnoreAbove(256))))
                .Add("idx_string", t => t.Match("*-s").Mapping(m => m.Keyword(s => s.IgnoreAbove(1024)))))
            .Properties(p => p
                .SetupDefaults()
                .Keyword(e => e.OrganizationId)
                    .FieldAlias(Alias.OrganizationId, a => a.Path(f => f.OrganizationId))
                .Keyword(e => e.ProjectId)
                    .FieldAlias(Alias.ProjectId, a => a.Path(f => f.ProjectId))
                .Keyword(e => e.StackId)
                    .FieldAlias(Alias.StackId, a => a.Path(f => f.StackId))
                .Keyword(e => e.ReferenceId)
                    .FieldAlias(Alias.ReferenceId, a => a.Path(f => f.ReferenceId))
                .Text(e => e.Type, t => t.Analyzer(LOWER_KEYWORD_ANALYZER).AddKeywordField())
                .Text(e => e.Source, t => t.Analyzer(STANDARDPLUS_ANALYZER).SearchAnalyzer(WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
                .Date(e => e.Date)
                .Text(e => e.Message)
                .Text(e => e.Tags, t => t.Analyzer(LOWER_KEYWORD_ANALYZER).AddKeywordField())
                    .FieldAlias(Alias.Tags, a => a.Path(f => f.Tags))
                .GeoPoint(e => e.Geo)
                .DoubleNumber(e => e.Value)
                .IntegerNumber(e => e.Count)
                .Boolean(e => e.IsFirstOccurrence)
                    .FieldAlias(Alias.IsFirstOccurrence, a => a.Path(f => f.IsFirstOccurrence))
                .Boolean(e => e.IsRegression)
                .Boolean(e => e.IngestionIsRegressionCandidate)
                .Keyword(e => e.IngestionRegressionFixedInVersion)
                .Date(e => e.IngestionRegressionDateFixed)
                .Object(e => e.Idx, o => o.Dynamic(DynamicMapping.True))
                .Object(e => e.Data, o => o.Properties(p2 => p2
                    .AddVersionMapping<PersistentEvent>()
                    .AddLevelMapping<PersistentEvent>()
                    .AddSubmissionMethodMapping<PersistentEvent>()
                    .AddSubmissionClientMapping<PersistentEvent>()
                    .AddLocationMapping<PersistentEvent>()
                    .AddRequestInfoMapping<PersistentEvent>()
                    .AddErrorMapping<PersistentEvent>()
                    .AddSimpleErrorMapping<PersistentEvent>()
                    .AddEnvironmentInfoMapping<PersistentEvent>()
                    .AddUserDescriptionMapping<PersistentEvent>()
                    .AddUserInfoMapping<PersistentEvent>()))
                .AddCopyToMappings()
                .AddDataDictionaryAliases()
        );

        if (Options is not null && Options.EnableMapperSizePlugin)
            map.Size(s => s.Enabled(true));
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor idx)
    {
        base.ConfigureIndex(idx);
        idx.Settings(s => s
            .Analysis(a => BuildAnalysis(a))
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .AddOtherSetting("index.mapping.total_fields.limit", _configuration.Options.FieldsLimit)
            .AddOtherSetting("index.mapping.ignore_malformed", true)
            .Priority(1));
    }

    public override async Task ConfigureAsync()
    {
        const string pipeline = "events-pipeline";
        var response = await Configuration.Client.Ingest.PutPipelineAsync(pipeline, d => d
            .Processors(p => p.Script(s => s
                .Source(FLATTEN_ERRORS_SCRIPT.TrimScript()))));

        var logger = Configuration.LoggerFactory.CreateLogger<EventIndex>();
        logger.LogRequest(response);

        if (!response.IsValidResponse)
        {
            string errorMessage = response.DebugInformation;
            logger.LogError(response.ApiCallDetails.OriginalException, "Error creating the pipeline {Pipeline}: {Message}", pipeline, errorMessage);
            throw new ApplicationException($"Error creating the pipeline {pipeline}: {errorMessage}", response.ApiCallDetails.OriginalException);
        }

        await base.ConfigureAsync();
    }

    protected override void ConfigureQueryParser(ElasticQueryParserConfiguration config)
    {
        config
            .SetDefaultFields([
                "id",
                Alias.ReferenceId,
                "reference_id",
                "source",
                "message",
                "tags",
                Alias.RequestPath,
                Alias.ErrorCode,
                Alias.ErrorType,
                Alias.ErrorTargetType,
                Alias.ErrorTargetMethod,
                EventIndexExtensions.DataPath<UserDescription>(Event.KnownDataKeys.UserDescription, u => u.Description),
                EventIndexExtensions.DataPath<UserDescription>(Event.KnownDataKeys.UserDescription, u => u.EmailAddress),
                EventIndexExtensions.DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, u => u.Identity),
                EventIndexExtensions.DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, u => u.Name)
            ])
            .AddQueryVisitor(new EventFieldsQueryVisitor())
            .UseFieldMap(new Dictionary<string, string> {
                    { Alias.BrowserVersion, EventIndexExtensions.DataDictionaryPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Data, RequestInfo.KnownDataKeys.BrowserVersion) },
                    { Alias.BrowserMajorVersion, EventIndexExtensions.DataDictionaryPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Data, RequestInfo.KnownDataKeys.BrowserMajorVersion) },
                    { Alias.User, EventIndexExtensions.DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, u => u.Identity) },
                    { Alias.UserName, EventIndexExtensions.DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, u => u.Name) },
                    { Alias.UserEmail, EventIndexExtensions.DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, u => u.Identity) },
                    { Alias.UserDescription, EventIndexExtensions.DataPath<UserDescription>(Event.KnownDataKeys.UserDescription, u => u.Description) },
                    { Alias.OperatingSystemVersion, EventIndexExtensions.DataDictionaryPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Data, RequestInfo.KnownDataKeys.OSVersion) },
                    { Alias.OperatingSystemMajorVersion, EventIndexExtensions.DataDictionaryPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Data, RequestInfo.KnownDataKeys.OSMajorVersion) }
            });
    }

    public ElasticsearchOptions Options => _configuration.Options;

    private void BuildAnalysis(IndexSettingsAnalysisDescriptor ad)
    {
        ad.Analyzers(a => a
            .Pattern(COMMA_WHITESPACE_ANALYZER, p => p.Pattern(@"[,\s]+"))
            .Custom(EMAIL_ANALYZER, c => c.Filter(EMAIL_TOKEN_FILTER, "lowercase", TLD_STOPWORDS_TOKEN_FILTER, EDGE_NGRAM_TOKEN_FILTER, "unique").Tokenizer("keyword"))
            .Custom(VERSION_INDEX_ANALYZER, c => c.Filter(VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, VERSION_TOKEN_FILTER, "lowercase", "unique").Tokenizer("whitespace"))
            .Custom(VERSION_SEARCH_ANALYZER, c => c.Filter(VERSION_PAD1_TOKEN_FILTER, VERSION_PAD2_TOKEN_FILTER, VERSION_PAD3_TOKEN_FILTER, VERSION_PAD4_TOKEN_FILTER, "lowercase").Tokenizer("whitespace"))
            .Custom(WHITESPACE_LOWERCASE_ANALYZER, c => c.Filter("lowercase").Tokenizer(COMMA_WHITESPACE_TOKENIZER))
            .Custom(TYPENAME_ANALYZER, c => c.Filter(TYPENAME_TOKEN_FILTER, "lowercase", "unique").Tokenizer(TYPENAME_HIERARCHY_TOKENIZER))
            .Custom(STANDARDPLUS_ANALYZER, c => c.Filter(STANDARDPLUS_TOKEN_FILTER, "lowercase", "unique").Tokenizer(COMMA_WHITESPACE_TOKENIZER))
            .Custom(LOWER_KEYWORD_ANALYZER, c => c.Filter("lowercase").Tokenizer("keyword"))
            .Custom(HOST_ANALYZER, c => c.Filter("lowercase").Tokenizer(HOST_TOKENIZER))
            .Custom(URL_PATH_ANALYZER, c => c.Filter("lowercase").Tokenizer(URL_PATH_TOKENIZER)))
        .TokenFilters(f => f
            .EdgeNGram(EDGE_NGRAM_TOKEN_FILTER, p => p.MaxGram(50).MinGram(2).Side(Elastic.Clients.Elasticsearch.Analysis.EdgeNGramSide.Front))
            .PatternCapture(EMAIL_TOKEN_FILTER, p => p.PreserveOriginal(true).Patterns("(\\w+)", "(\\p{L}+)", "(\\d+)", "@(.+)", "@(.+)\\.", "(.+)@"))
            .PatternCapture(STANDARDPLUS_TOKEN_FILTER, p => p.PreserveOriginal(true).Patterns(
                @"([^\.\(\)\[\]\/\\\{\}\?=&;:\<\>]+)",
                @"([^\.\(\)\[\]\/\\\{\}\?=&;:\<\>]+[\.\/\\][^\.\(\)\[\]\/\\\{\}\?=&;:\<\>]+)",
                @"([^\.\(\)\[\]\/\\\{\}\?=&;:\<\>]+[\.\/\\][^\.\(\)\[\]\/\\\{\}\?=&;:\<\>]+[\.\/\\][^\.\(\)\[\]\/\\\{\}\?=&;:\<\>]+)"
            ))
            .PatternCapture(TYPENAME_TOKEN_FILTER, p => p.PreserveOriginal(true).Patterns(@" ^ (\w+)", @"\.(\w+)", @"([^\(\)]+)"))
            .PatternCapture(VERSION_TOKEN_FILTER, p => p.Patterns(@"^(\d+)\.", @"^(\d+\.\d+)", @"^(\d+\.\d+\.\d+)"))
            .PatternReplace(VERSION_PAD1_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{1})(?=\.|-|$)").Replacement("$10000$2"))
            .PatternReplace(VERSION_PAD2_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{2})(?=\.|-|$)").Replacement("$1000$2"))
            .PatternReplace(VERSION_PAD3_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{3})(?=\.|-|$)").Replacement("$100$2"))
            .PatternReplace(VERSION_PAD4_TOKEN_FILTER, p => p.Pattern(@"(\.|^)(\d{4})(?=\.|-|$)").Replacement("$10$2"))
            .Stop(TLD_STOPWORDS_TOKEN_FILTER, p => p.Stopwords(new string[] { "com", "net", "org", "info", "me", "edu", "mil", "gov", "biz", "co", "io", "dev" }))
            .WordDelimiter(ALL_WORDS_DELIMITER_TOKEN_FILTER, p => p.CatenateNumbers(true).PreserveOriginal(true).CatenateAll(true).CatenateWords(true)))
        .Tokenizers(t => t
            .CharGroup(COMMA_WHITESPACE_TOKENIZER, p => p.TokenizeOnChars(",", "whitespace"))
            .CharGroup(URL_PATH_TOKENIZER, p => p.TokenizeOnChars("/", "-", "."))
            .CharGroup(HOST_TOKENIZER, p => p.TokenizeOnChars("."))
            .PathHierarchy(TYPENAME_HIERARCHY_TOKENIZER, p => p.Delimiter(".")));
    }

    private const string ALL_WORDS_DELIMITER_TOKEN_FILTER = "all_word_delimiter";
    private const string EDGE_NGRAM_TOKEN_FILTER = "edge_ngram";
    private const string EMAIL_TOKEN_FILTER = "email";
    private const string TYPENAME_TOKEN_FILTER = "typename";
    private const string VERSION_TOKEN_FILTER = "version";
    private const string STANDARDPLUS_TOKEN_FILTER = "standardplus";
    private const string VERSION_PAD1_TOKEN_FILTER = "version_pad1";
    private const string VERSION_PAD2_TOKEN_FILTER = "version_pad2";
    private const string VERSION_PAD3_TOKEN_FILTER = "version_pad3";
    private const string VERSION_PAD4_TOKEN_FILTER = "version_pad4";
    private const string TLD_STOPWORDS_TOKEN_FILTER = "tld_stopwords";

    internal const string COMMA_WHITESPACE_ANALYZER = "comma_whitespace";
    internal const string EMAIL_ANALYZER = "email";
    internal const string VERSION_INDEX_ANALYZER = "version_index";
    internal const string VERSION_SEARCH_ANALYZER = "version_search";
    internal const string WHITESPACE_LOWERCASE_ANALYZER = "whitespace_lower";
    internal const string TYPENAME_ANALYZER = "typename";
    internal const string STANDARDPLUS_ANALYZER = "standardplus";
    internal const string LOWER_KEYWORD_ANALYZER = "lowerkeyword";
    internal const string URL_PATH_ANALYZER = "urlpath";
    internal const string HOST_ANALYZER = "hostname";

    private const string COMMA_WHITESPACE_TOKENIZER = "comma_whitespace";
    private const string URL_PATH_TOKENIZER = "urlpath";
    private const string HOST_TOKENIZER = "hostname";
    private const string TYPENAME_HIERARCHY_TOKENIZER = "typename_hierarchy";

    private const string FLATTEN_ERRORS_SCRIPT = @"
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

    public sealed class Alias
    {
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

        public const string Error = "error";
        public const string ErrorCode = "error.code";
        public const string ErrorType = "error.type";
        public const string ErrorMessage = "error.message";
        public const string ErrorTargetType = "error.targettype";
        public const string ErrorTargetMethod = "error.targetmethod";
    }
}

internal static class EventIndexExtensions
{
    private static readonly JsonSerializerOptions _propertyNameOptions = new JsonSerializerOptions().ConfigureExceptionlessDefaults();

    public static PropertiesDescriptor<PersistentEvent> AddCopyToMappings(this PropertiesDescriptor<PersistentEvent> descriptor)
    {
        return descriptor
            .Text(EventIndex.Alias.IpAddress, t => t.Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER).AddKeywordField())
            .Text(EventIndex.Alias.OperatingSystem, t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
            .Object(EventIndex.Alias.Error, o => o.Properties(p1 => p1
                .Keyword("code", k => k.IgnoreAbove(1024))
                .Text("message", t => t.AddKeywordField())
                .Text("type", t => t.Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
                .Text("targettype", t => t.Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
                .Text("targetmethod", t => t.Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())));
    }

    public static PropertiesDescriptor<PersistentEvent> AddDataDictionaryAliases(this PropertiesDescriptor<PersistentEvent> descriptor)
    {
        return descriptor
            .FieldAlias(EventIndex.Alias.Version, a => a.Path($"data.{Event.KnownDataKeys.Version}"))
            .FieldAlias(EventIndex.Alias.Level, a => a.Path($"data.{Event.KnownDataKeys.Level}"))
            .FieldAlias(EventIndex.Alias.SubmissionMethod, a => a.Path($"data.{Event.KnownDataKeys.SubmissionMethod}"))
            .FieldAlias(EventIndex.Alias.ClientUserAgent, a => a.Path(DataPath<SubmissionClient>(Event.KnownDataKeys.SubmissionClient, c => c.UserAgent)))
            .FieldAlias(EventIndex.Alias.ClientVersion, a => a.Path(DataPath<SubmissionClient>(Event.KnownDataKeys.SubmissionClient, c => c.Version)))
            .FieldAlias(EventIndex.Alias.LocationCountry, a => a.Path(DataPath<Location>(Event.KnownDataKeys.Location, l => l.Country)))
            .FieldAlias(EventIndex.Alias.LocationLevel1, a => a.Path(DataPath<Location>(Event.KnownDataKeys.Location, l => l.Level1)))
            .FieldAlias(EventIndex.Alias.LocationLevel2, a => a.Path(DataPath<Location>(Event.KnownDataKeys.Location, l => l.Level2)))
            .FieldAlias(EventIndex.Alias.LocationLocality, a => a.Path(DataPath<Location>(Event.KnownDataKeys.Location, l => l.Locality)))
            .FieldAlias(EventIndex.Alias.Browser, a => a.Path(DataDictionaryPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Data, RequestInfo.KnownDataKeys.Browser)))
            .FieldAlias(EventIndex.Alias.Device, a => a.Path(DataDictionaryPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Data, RequestInfo.KnownDataKeys.Device)))
            .FieldAlias(EventIndex.Alias.RequestIsBot, a => a.Path(DataDictionaryPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Data, RequestInfo.KnownDataKeys.IsBot)))
            .FieldAlias(EventIndex.Alias.RequestPath, a => a.Path(DataPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Path)))
            .FieldAlias(EventIndex.Alias.RequestUserAgent, a => a.Path(DataPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.UserAgent)))
            .FieldAlias(EventIndex.Alias.CommandLine, a => a.Path(DataPath<EnvironmentInfo>(Event.KnownDataKeys.EnvironmentInfo, e => e.CommandLine)))
            .FieldAlias(EventIndex.Alias.MachineArchitecture, a => a.Path(DataPath<EnvironmentInfo>(Event.KnownDataKeys.EnvironmentInfo, e => e.Architecture)))
            .FieldAlias(EventIndex.Alias.MachineName, a => a.Path(DataPath<EnvironmentInfo>(Event.KnownDataKeys.EnvironmentInfo, e => e.MachineName)));
    }

    public static PropertiesDescriptor<T> AddVersionMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Text(Event.KnownDataKeys.Version, t => t.Analyzer(EventIndex.VERSION_INDEX_ANALYZER).SearchAnalyzer(EventIndex.VERSION_SEARCH_ANALYZER).AddKeywordField());
    }

    public static PropertiesDescriptor<T> AddLevelMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Text(Event.KnownDataKeys.Level, t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField());
    }

    public static PropertiesDescriptor<T> AddSubmissionMethodMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Keyword(Event.KnownDataKeys.SubmissionMethod, k => k.IgnoreAbove(1024));
    }

    public static PropertiesDescriptor<T> AddSubmissionClientMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.SubmissionClient, o => o.Properties(p3 => p3
            .Text(Field<SubmissionClient>(c => c.IpAddress), t => t.Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER).CopyTo(EventIndex.Alias.IpAddress))
            .Text(Field<SubmissionClient>(c => c.UserAgent), t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField())
            .Keyword(Field<SubmissionClient>(c => c.Version), k => k.IgnoreAbove(1024))));
    }

    public static PropertiesDescriptor<T> AddLocationMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.Location, o => o.Properties(p3 => p3
            .Text(Field<Location>(l => l.Country), t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField())
            .Keyword(Field<Location>(l => l.Level1), k => k.IgnoreAbove(1024))
            .Keyword(Field<Location>(l => l.Level2), k => k.IgnoreAbove(1024))
            .Keyword(Field<Location>(l => l.Locality), k => k.IgnoreAbove(1024))));
    }

    public static PropertiesDescriptor<T> AddRequestInfoMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.RequestInfo, o => o.Properties(p3 => p3
            .Text(Field<RequestInfo>(r => r.ClientIpAddress), t => t.Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER).CopyTo(EventIndex.Alias.IpAddress))
            .Text(Field<RequestInfo>(r => r.UserAgent), t => t.AddKeywordField())
            .Text(Field<RequestInfo>(r => r.Path), t => t.Analyzer(EventIndex.URL_PATH_ANALYZER).AddKeywordField())
            .Text(Field<RequestInfo>(r => r.Host), t => t.Analyzer(EventIndex.HOST_ANALYZER).AddKeywordField())
            .IntegerNumber(Field<RequestInfo>(r => r.Port))
            .Keyword(Field<RequestInfo>(r => r.HttpMethod))
            .Object(Field<RequestInfo>(r => r.Data), oi => oi.Properties(p4 => p4
                .Text(RequestInfo.KnownDataKeys.Browser, t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
                .Keyword(RequestInfo.KnownDataKeys.BrowserVersion, k => k.IgnoreAbove(1024))
                .Keyword(RequestInfo.KnownDataKeys.BrowserMajorVersion, k => k.IgnoreAbove(1024))
                .Text(RequestInfo.KnownDataKeys.Device, t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
                .Text(RequestInfo.KnownDataKeys.OS, t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField().CopyTo(EventIndex.Alias.OperatingSystem).Index(false))
                .Keyword(RequestInfo.KnownDataKeys.OSVersion, k => k.IgnoreAbove(1024))
                .Keyword(RequestInfo.KnownDataKeys.OSMajorVersion, k => k.IgnoreAbove(1024))
                .Boolean(RequestInfo.KnownDataKeys.IsBot)))));
    }

    public static PropertiesDescriptor<T> AddErrorMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.Error, o => o.Properties(p3 => p3
            .Object(Field<Error>(e => e.Data), oi => oi.Properties(p4 => p4
                .Object(Error.KnownDataKeys.TargetInfo, oi2 => oi2.Properties(p5 => p5
                    .Keyword("ExceptionType", k => k.IgnoreAbove(1024).CopyTo(EventIndex.Alias.ErrorTargetType))
                    .Keyword("Method", k => k.IgnoreAbove(1024).CopyTo(EventIndex.Alias.ErrorTargetMethod))))))));
    }

    public static PropertiesDescriptor<T> AddSimpleErrorMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.SimpleError, o => o.Properties(p3 => p3
            .Object(Field<SimpleError>(e => e.Data), oi => oi.Properties(p4 => p4
                .Object(Error.KnownDataKeys.TargetInfo, oi2 => oi2.Properties(p5 => p5
                    .Keyword("ExceptionType", k => k.IgnoreAbove(1024).CopyTo(EventIndex.Alias.ErrorTargetType))))))));
    }

    public static PropertiesDescriptor<T> AddEnvironmentInfoMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.EnvironmentInfo, o => o.Properties(p3 => p3
            .Text(Field<EnvironmentInfo>(e => e.IpAddress), t => t.Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER).CopyTo(EventIndex.Alias.IpAddress))
            .Text(Field<EnvironmentInfo>(e => e.MachineName), t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField())
            .Text(Field<EnvironmentInfo>(e => e.OSName), t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField().CopyTo(EventIndex.Alias.OperatingSystem))
            .Keyword(Field<EnvironmentInfo>(e => e.CommandLine), k => k.IgnoreAbove(1024))
            .Keyword(Field<EnvironmentInfo>(e => e.Architecture), k => k.IgnoreAbove(1024))));
    }

    public static PropertiesDescriptor<T> AddUserDescriptionMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.UserDescription, o => o.Properties(p3 => p3
            .Text(Field<UserDescription>(u => u.Description))
            .Text(Field<UserDescription>(u => u.EmailAddress), t => t.Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer("simple").AddKeywordField().CopyTo(DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, u => u.Identity)))));
    }

    public static PropertiesDescriptor<T> AddUserInfoMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.UserInfo, o => o.Properties(p3 => p3
            .Text(Field<UserInfo>(u => u.Identity), t => t.Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
            .Text(Field<UserInfo>(u => u.Name), t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField())));
    }

    public static string DataPath<TModel>(string dataKey, Expression<Func<TModel, object?>> property)
    {
        return $"data.{dataKey}.{Field(property)}";
    }

    public static string DataDictionaryPath<TModel>(string dataKey, Expression<Func<TModel, object?>> dictionaryProperty, string dictionaryKey)
    {
        return $"{DataPath(dataKey, dictionaryProperty)}.{dictionaryKey}";
    }

    private static string Field<TModel>(Expression<Func<TModel, object?>> property)
    {
        string propertyName = GetPropertyInfo(property).Name;
        JsonTypeInfo typeInfo = _propertyNameOptions.GetTypeInfo(typeof(TModel));

        foreach (JsonPropertyInfo jsonProperty in typeInfo.Properties)
        {
            if (jsonProperty.AttributeProvider is PropertyInfo modelProperty && String.Equals(modelProperty.Name, propertyName, StringComparison.Ordinal))
                return jsonProperty.Name;
        }

        throw new InvalidOperationException($"Unable to resolve JSON field name for {typeof(TModel).FullName}.{propertyName}.");
    }

    private static PropertyInfo GetPropertyInfo<TModel>(Expression<Func<TModel, object?>> expression)
    {
        Expression body = expression.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary
            ? unary.Operand
            : expression.Body;

        if (body is MemberExpression { Member: PropertyInfo property })
            return property;

        throw new ArgumentException("Expression must select a model property.", nameof(expression));
    }
}
