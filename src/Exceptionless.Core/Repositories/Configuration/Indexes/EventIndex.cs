using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories.Queries;
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
                .Keyword(e => e.Id)
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

        // SizeField is not available in the v8 Elastic client
        // if (Options is not null && Options.EnableMapperSizePlugin)
        //     map.SizeField(s => s.Enabled(true));
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor idx)
    {
        base.ConfigureIndex(idx);
        idx.Settings(s => s
            .Analysis(a => BuildAnalysis(a))
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .AddOtherSetting("index.mapping.total_fields.limit", _configuration.Options.FieldsLimit.ToString())
            .AddOtherSetting("index.mapping.ignore_malformed", "true")
            .Priority(1));
    }

    public override async Task ConfigureAsync()
    {
        const string pipeline = "events-pipeline";
        var response = await Configuration.Client.Ingest.PutPipelineAsync(pipeline, d => d
            .Processors(p => p.Script(s => s
                .Source(FLATTEN_ERRORS_SCRIPT.Replace("\r", String.Empty).Replace("\n", String.Empty).Replace("  ", " ")))));

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
                "source",
                "message",
                "tags",
                "path",
                "error.code",
                "error.type",
                "error.targettype",
                "error.targetmethod",
                $"data.{Event.KnownDataKeys.UserDescription}.description",
                $"data.{Event.KnownDataKeys.UserDescription}.email_address",
                $"data.{Event.KnownDataKeys.UserInfo}.identity",
                $"data.{Event.KnownDataKeys.UserInfo}.name"
            ])
            .AddQueryVisitor(new EventFieldsQueryVisitor())
            .UseFieldMap(new Dictionary<string, string> {
                    { Alias.BrowserVersion, $"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.BrowserVersion}" },
                    { Alias.BrowserMajorVersion, $"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.BrowserMajorVersion}" },
                    { Alias.User, $"data.{Event.KnownDataKeys.UserInfo}.identity" },
                    { Alias.UserName, $"data.{Event.KnownDataKeys.UserInfo}.name" },
                    { Alias.UserEmail, $"data.{Event.KnownDataKeys.UserInfo}.identity" },
                    { Alias.UserDescription, $"data.{Event.KnownDataKeys.UserDescription}.description" },
                    { Alias.OperatingSystemVersion, $"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.OSVersion}" },
                    { Alias.OperatingSystemMajorVersion, $"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.OSMajorVersion}" }
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
    public static PropertiesDescriptor<PersistentEvent> AddCopyToMappings(this PropertiesDescriptor<PersistentEvent> descriptor)
    {
        return descriptor
            .Text(EventIndex.Alias.IpAddress, t => t.Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER))
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
            .FieldAlias(EventIndex.Alias.ClientUserAgent, a => a.Path($"data.{Event.KnownDataKeys.SubmissionClient}.user_agent"))
            .FieldAlias(EventIndex.Alias.ClientVersion, a => a.Path($"data.{Event.KnownDataKeys.SubmissionClient}.version"))
            .FieldAlias(EventIndex.Alias.LocationCountry, a => a.Path($"data.{Event.KnownDataKeys.Location}.country"))
            .FieldAlias(EventIndex.Alias.LocationLevel1, a => a.Path($"data.{Event.KnownDataKeys.Location}.level1"))
            .FieldAlias(EventIndex.Alias.LocationLevel2, a => a.Path($"data.{Event.KnownDataKeys.Location}.level2"))
            .FieldAlias(EventIndex.Alias.LocationLocality, a => a.Path($"data.{Event.KnownDataKeys.Location}.locality"))
            .FieldAlias(EventIndex.Alias.Browser, a => a.Path($"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.Browser}"))
            .FieldAlias(EventIndex.Alias.Device, a => a.Path($"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.Device}"))
            .FieldAlias(EventIndex.Alias.RequestIsBot, a => a.Path($"data.{Event.KnownDataKeys.RequestInfo}.data.{RequestInfo.KnownDataKeys.IsBot}"))
            .FieldAlias(EventIndex.Alias.RequestPath, a => a.Path($"data.{Event.KnownDataKeys.RequestInfo}.path"))
            .FieldAlias(EventIndex.Alias.RequestUserAgent, a => a.Path($"data.{Event.KnownDataKeys.RequestInfo}.user_agent"))
            .FieldAlias(EventIndex.Alias.CommandLine, a => a.Path($"data.{Event.KnownDataKeys.EnvironmentInfo}.command_line"))
            .FieldAlias(EventIndex.Alias.MachineArchitecture, a => a.Path($"data.{Event.KnownDataKeys.EnvironmentInfo}.architecture"))
            .FieldAlias(EventIndex.Alias.MachineName, a => a.Path($"data.{Event.KnownDataKeys.EnvironmentInfo}.machine_name"));
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
            .Text("ip_address", t => t.Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER).CopyTo(EventIndex.Alias.IpAddress))
            .Text("user_agent", t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField())
            .Keyword("version", k => k.IgnoreAbove(1024))));
    }

    public static PropertiesDescriptor<T> AddLocationMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.Location, o => o.Properties(p3 => p3
            .Text("country", t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField())
            .Keyword("level1", k => k.IgnoreAbove(1024))
            .Keyword("level2", k => k.IgnoreAbove(1024))
            .Keyword("locality", k => k.IgnoreAbove(1024))));
    }

    public static PropertiesDescriptor<T> AddRequestInfoMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.RequestInfo, o => o.Properties(p3 => p3
            .Text("client_ip_address", t => t.Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER).CopyTo(EventIndex.Alias.IpAddress))
            .Text("user_agent", t => t.AddKeywordField())
            .Text("path", t => t.Analyzer(EventIndex.URL_PATH_ANALYZER).AddKeywordField())
            .Text("host", t => t.Analyzer(EventIndex.HOST_ANALYZER).AddKeywordField())
            .IntegerNumber("port")
            .Keyword("http_method")
            .Object("data", oi => oi.Properties(p4 => p4
                .Text(RequestInfo.KnownDataKeys.Browser, t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
                .Keyword(RequestInfo.KnownDataKeys.BrowserVersion, k => k.IgnoreAbove(1024))
                .Keyword(RequestInfo.KnownDataKeys.BrowserMajorVersion, k => k.IgnoreAbove(1024))
                .Text(RequestInfo.KnownDataKeys.Device, t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
                .Text(RequestInfo.KnownDataKeys.OS, t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField().CopyTo(EventIndex.Alias.OperatingSystem))
                .Keyword(RequestInfo.KnownDataKeys.OSVersion, k => k.IgnoreAbove(1024))
                .Keyword(RequestInfo.KnownDataKeys.OSMajorVersion, k => k.IgnoreAbove(1024))
                .Boolean(RequestInfo.KnownDataKeys.IsBot)))));
    }

    public static PropertiesDescriptor<T> AddErrorMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.Error, o => o.Properties(p3 => p3
            .Object("data", oi => oi.Properties(p4 => p4
                .Object(Error.KnownDataKeys.TargetInfo, oi2 => oi2.Properties(p5 => p5
                    .Keyword("ExceptionType", k => k.IgnoreAbove(1024).CopyTo(EventIndex.Alias.ErrorTargetType))
                    .Keyword("Method", k => k.IgnoreAbove(1024).CopyTo(EventIndex.Alias.ErrorTargetMethod))))))));
    }

    public static PropertiesDescriptor<T> AddSimpleErrorMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.SimpleError, o => o.Properties(p3 => p3
            .Object("data", oi => oi.Properties(p4 => p4
                .Object(Error.KnownDataKeys.TargetInfo, oi2 => oi2.Properties(p5 => p5
                    .Keyword("ExceptionType", k => k.IgnoreAbove(1024).CopyTo(EventIndex.Alias.ErrorTargetType))))))));
    }

    public static PropertiesDescriptor<T> AddEnvironmentInfoMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.EnvironmentInfo, o => o.Properties(p3 => p3
            .Text("ip_address", t => t.Analyzer(EventIndex.COMMA_WHITESPACE_ANALYZER).CopyTo(EventIndex.Alias.IpAddress))
            .Text("machine_name", t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField())
            .Text("o_s_name", t => t.Analyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField().CopyTo(EventIndex.Alias.OperatingSystem))
            .Keyword("command_line", k => k.IgnoreAbove(1024))
            .Keyword("architecture", k => k.IgnoreAbove(1024))));
    }

    public static PropertiesDescriptor<T> AddUserDescriptionMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.UserDescription, o => o.Properties(p3 => p3
            .Text("description")
            .Text("email_address", t => t.Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer("simple").AddKeywordField().CopyTo($"data.{Event.KnownDataKeys.UserInfo}.identity"))));
    }

    public static PropertiesDescriptor<T> AddUserInfoMapping<T>(this PropertiesDescriptor<T> descriptor) where T : class
    {
        return descriptor.Object(Event.KnownDataKeys.UserInfo, o => o.Properties(p3 => p3
            .Text("identity", t => t.Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).AddKeywordField())
            .Text("name", t => t.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER).AddKeywordField())));
    }
}
