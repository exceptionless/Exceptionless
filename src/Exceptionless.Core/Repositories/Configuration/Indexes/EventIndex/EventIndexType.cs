using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class EventIndexType : MonthlyIndexType<PersistentEvent> {
        public EventIndexType(EventIndex index) : base(index, "events", document => document.Date.UtcDateTime) {}

        public override TypeMappingDescriptor<PersistentEvent> BuildMapping(TypeMappingDescriptor<PersistentEvent> map) {
            return base.BuildMapping(map)
                .Dynamic(false)
                .DynamicTemplates(dt => dt.DynamicTemplate("idx_reference", t => t.Match("*-r").Mapping(m => m.Keyword(s => s.IgnoreAbove(256)))))
                .SizeField(s => s.Enabled())
                //.Transform(t => t.Add(a => a.Script(FLATTEN_ERRORS_SCRIPT).Language(ScriptLang.Groovy)))
                .AllField(a => a.Enabled(false).Analyzer(EventIndex.STANDARDPLUS_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER))
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
                    .Scalar(f => f.Value, f => f.Alias(Alias.Value))
                    .Scalar(f => f.Count, f => f.Alias(Alias.Count))
                    .Boolean(f => f.Name(e => e.IsFirstOccurrence).Alias(Alias.IsFirstOccurrence))
                    .Boolean(f => f.Name(e => e.IsFixed).Alias(Alias.IsFixed))
                    .Boolean(f => f.Name(e => e.IsHidden).Alias(Alias.IsHidden))
                    .Object<object>(f => f.Name(e => e.Idx).Alias(Alias.IDX).Dynamic(true))
                    .Ip(f => f.Name(Alias.IpAddress).IncludeInAll())
                    .Text(f => f.Name(Alias.OperatingSystem))
                    .AddDataDictionaryMappings()
            );
        }

        protected override void ConfigureQueryBuilder(ElasticQueryBuilder builder) {
            builder.UseQueryParser(this);
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
            public const string BrowserVersion = "browser.version";
            public const string BrowserMajorVersion = "browser.major";
            public const string RequestIsBot = "bot";

            public const string Device = "device";

            public const string OperatingSystem = "os";
            public const string OperatingSystemVersion = "os.version";
            public const string OperatingSystemMajorVersion = "os.major";

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

    internal static class EventIndexTypeExtensions {
        public static PropertiesDescriptor<PersistentEvent> AddDataDictionaryMappings(this PropertiesDescriptor<PersistentEvent> descriptor) {
            return descriptor.Object<DataDictionary>(f => f.Name(e => e.Data).Properties(p2 => p2
                .AddVersionMapping()
                .AddLevelMapping()
                .AddSubmissionMethodMapping()
                .AddLocationMapping()
                .AddRequestInfoMapping()
                .AddErrorMapping()
                .AddSimpleErrorMapping()
                .AddEnvironmentInfoMapping()
                .AddUserDescriptionMapping()
                .AddUserInfoMapping())
                );
        }

        public static PropertiesDescriptor<DataDictionary> AddVersionMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.Version).RootAlias(EventIndexType.Alias.Version).Analyzer(EventIndex.VERSION_INDEX_ANALYZER).SearchAnalyzer(EventIndex.VERSION_SEARCH_ANALYZER).AddKeywordField());
        }

        public static PropertiesDescriptor<DataDictionary> AddLevelMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.Level).RootAlias(EventIndexType.Alias.Level).AddKeywordField());
        }

        public static PropertiesDescriptor<DataDictionary> AddSubmissionMethodMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Text(f2 => f2.Name(Event.KnownDataKeys.SubmissionMethod).RootAlias(EventIndexType.Alias.SubmissionMethod).AddKeywordField());
        }

        public static PropertiesDescriptor<DataDictionary> AddLocationMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<Location>(f2 => f2.Name(Event.KnownDataKeys.Location).Properties(p3 => p3
                .Keyword(f3 => f3.Name(r => r.Country).RootAlias(EventIndexType.Alias.LocationCountry))
                .Keyword(f3 => f3.Name(r => r.Level1).RootAlias(EventIndexType.Alias.LocationLevel1))
                .Keyword(f3 => f3.Name(r => r.Level2).RootAlias(EventIndexType.Alias.LocationLevel2))
                .Keyword(f3 => f3.Name(r => r.Locality).RootAlias(EventIndexType.Alias.LocationLocality))));
        }

        public static PropertiesDescriptor<DataDictionary> AddRequestInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<RequestInfo>(f2 => f2.Name(Event.KnownDataKeys.RequestInfo).Properties(p3 => p3
                //.Ip(f3 => f3.Name(r => r.ClientIpAddress).CopyTo(fd => fd.Field(EventIndexType.Alias.IpAddress)).Index(false))
                .Text(f3 => f3.Name(r => r.UserAgent).RootAlias(EventIndexType.Alias.RequestUserAgent).AddKeywordField())
                .Text(f3 => f3.Name(r => r.Path).RootAlias(EventIndexType.Alias.RequestPath).IncludeInAll().AddKeywordField())
                .Object<DataDictionary>(f3 => f3.Name(e => e.Data).Properties(p4 => p4
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.Browser).RootAlias(EventIndexType.Alias.Browser).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserVersion).RootAlias(EventIndexType.Alias.BrowserVersion).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.BrowserMajorVersion).RootAlias(EventIndexType.Alias.BrowserMajorVersion))
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.Device).RootAlias(EventIndexType.Alias.Device).AddKeywordField())
                    //.Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OS).CopyTo(fd => fd.Field(EventIndexType.Alias.OperatingSystem)).Index(false))
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OSVersion).RootAlias(EventIndexType.Alias.OperatingSystemVersion).AddKeywordField())
                    .Text(f4 => f4.Name(RequestInfo.KnownDataKeys.OSMajorVersion).RootAlias(EventIndexType.Alias.OperatingSystemMajorVersion))
                    .Boolean(f4 => f4.Name(RequestInfo.KnownDataKeys.IsBot).RootAlias(EventIndexType.Alias.RequestIsBot))))));
        }

        public static PropertiesDescriptor<DataDictionary> AddErrorMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<Error>(f2 => f2.Name(Event.KnownDataKeys.Error).Properties(p3 => p3
                .Keyword(f3 => f3.Name("all_codes").RootAlias(EventIndexType.Alias.ErrorCode).IncludeInAll().Boost(1.1))
                .Text(f3 => f3.Name("all_messages").RootAlias(EventIndexType.Alias.ErrorMessage).IncludeInAll().AddKeywordField())
                .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Properties(p4 => p4
                    .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Properties(p5 => p5
                        .Text(f6 => f6.Name("ExceptionType").RootAlias(EventIndexType.Alias.ErrorTargetType).Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2).AddKeywordField())
                        .Text(f6 => f6.Name("Method").RootAlias(EventIndexType.Alias.ErrorTargetMethod).Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2).AddKeywordField())))))
                .Text(f3 => f3.Name("all_types").RootAlias(EventIndexType.Alias.ErrorType).Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1).AddKeywordField())));
        }

        public static PropertiesDescriptor<DataDictionary> AddSimpleErrorMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<SimpleError>(f2 => f2.Name(Event.KnownDataKeys.SimpleError).Properties(p3 => p3
                .Text(f3 => f3.Name("all_messages").RootAlias(EventIndexType.Alias.ErrorMessage + "s").IncludeInAll().AddKeywordField())
                .Object<DataDictionary>(f4 => f4.Name(e => e.Data).Properties(p4 => p4
                    .Object<object>(f5 => f5.Name(Error.KnownDataKeys.TargetInfo).Properties(p5 => p5
                        .Text(f6 => f6.Name("ExceptionType").RootAlias(EventIndexType.Alias.ErrorTargetType + "s").Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.2).AddKeywordField())))))
                .Text(f3 => f3.Name("all_types").RootAlias(EventIndexType.Alias.ErrorType + "s").Analyzer(EventIndex.TYPENAME_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1).AddKeywordField())));
        }

        public static PropertiesDescriptor<DataDictionary> AddEnvironmentInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<EnvironmentInfo>(f2 => f2.Name(Event.KnownDataKeys.EnvironmentInfo).Properties(p3 => p3
                .Ip(f3 => f3.Name(r => r.IpAddress).CopyTo(fd => fd.Field(EventIndexType.Alias.IpAddress)).Index(false))
                .Text(f3 => f3.Name(r => r.MachineName).RootAlias(EventIndexType.Alias.MachineName).IncludeInAll().Boost(1.1).AddKeywordField())
                .Text(f3 => f3.Name(r => r.OSName).CopyTo(fd => fd.Field(EventIndexType.Alias.OperatingSystem)))
                .Keyword(f3 => f3.Name(r => r.Architecture).RootAlias(EventIndexType.Alias.MachineArchitecture))
                ));
        }

        public static PropertiesDescriptor<DataDictionary> AddUserDescriptionMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<UserDescription>(f2 => f2.Name(Event.KnownDataKeys.UserDescription).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.Description).RootAlias(EventIndexType.Alias.UserDescription).IncludeInAll()) // TODO: Why is one simple and other lowercase.
                .Text(f3 => f3.Name(r => r.EmailAddress).RootAlias(EventIndexType.Alias.UserEmail).Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer("simple").IncludeInAll().Boost(1.1).AddKeywordField())));
        }

        public static PropertiesDescriptor<DataDictionary> AddUserInfoMapping(this PropertiesDescriptor<DataDictionary> descriptor) {
            return descriptor.Object<UserInfo>(f2 => f2.Name(Event.KnownDataKeys.UserInfo).Properties(p3 => p3
                .Text(f3 => f3.Name(r => r.Identity).RootAlias(EventIndexType.Alias.User).Analyzer(EventIndex.EMAIL_ANALYZER).SearchAnalyzer(EventIndex.WHITESPACE_LOWERCASE_ANALYZER).IncludeInAll().Boost(1.1).AddKeywordField())
                .Text(f3 => f3.Name(r => r.Name).RootAlias(EventIndexType.Alias.UserName).IncludeInAll().AddKeywordField())));
        }
    }
}