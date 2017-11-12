using System;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class EventIndex : DailyIndex {
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

        public EventIndex(IElasticConfiguration configuration) : base(configuration, Settings.Current.AppScopePrefix + "events", 1) {
            MaxIndexAge = TimeSpan.FromDays(180);

            AddType(Event = new EventIndexType(this));
            AddAlias($"{Name}-today", TimeSpan.FromDays(1));
            AddAlias($"{Name}-last3days", TimeSpan.FromDays(7));
            AddAlias($"{Name}-last7days", TimeSpan.FromDays(7));
            AddAlias($"{Name}-last30days", TimeSpan.FromDays(30));
            AddAlias($"{Name}-last90days", TimeSpan.FromDays(90));
        }

        public EventIndexType Event { get; }

        public override CreateIndexDescriptor ConfigureIndex(CreateIndexDescriptor idx) {
            return base.ConfigureIndex(idx.Settings(s => s
                .Analysis(BuildAnalysis)
                .NumberOfShards(Settings.Current.ElasticSearchNumberOfShards)
                .NumberOfReplicas(Settings.Current.ElasticSearchNumberOfReplicas)
                .Setting("index.mapping.total_fields.limit", Settings.Current.ElasticSearchFieldsLimit)
                .Priority(1)));
        }

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
    }
}
