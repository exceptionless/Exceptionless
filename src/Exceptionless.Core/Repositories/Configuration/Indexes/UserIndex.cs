using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;

namespace Exceptionless.Core.Repositories.Configuration;

public sealed class UserIndex : VersionedIndex<User>
{
    private const string KEYWORD_LOWERCASE_ANALYZER = "keyword_lowercase";
    private readonly ExceptionlessElasticConfiguration _configuration;

    public UserIndex(ExceptionlessElasticConfiguration configuration) : base(configuration, configuration.Options.ScopePrefix + "users", 1)
    {
        _configuration = configuration;
    }

    public override void ConfigureIndexMapping(TypeMappingDescriptor<User> map)
    {
        map
            .Dynamic(DynamicMapping.False)
            .Properties(p => p
                .SetupDefaults()
                .Keyword(e => e.OrganizationIds)
                .Text(e => e.FullName, t => t.AddKeywordField())
                .Text(e => e.EmailAddress, t => t.Analyzer(KEYWORD_LOWERCASE_ANALYZER).AddKeywordField())
                .Boolean(e => e.IsEmailAddressVerified)
                .Keyword(e => e.VerifyEmailAddressToken)
                .Date(e => e.VerifyEmailAddressTokenExpiration)
                .Keyword(e => e.PasswordResetToken)
                .Date(e => e.PasswordResetTokenExpiration)
                .Keyword(e => e.Roles)
                .Object(e => e.OAuthAccounts, o => o.Properties(mp => mp
                    .Keyword("provider")
                    .Keyword("provider_user_id")
                    .Keyword("username")))
            );
    }

    public override void ConfigureIndex(CreateIndexRequestDescriptor idx)
    {
        base.ConfigureIndex(idx);
        idx.Settings(s => s
            .Analysis(d => d.Analyzers(b => b.Custom(KEYWORD_LOWERCASE_ANALYZER, c => c.Filter("lowercase").Tokenizer("keyword"))))
            .NumberOfShards(_configuration.Options.NumberOfShards)
            .NumberOfReplicas(_configuration.Options.NumberOfReplicas)
            .Priority(5));
    }
}
