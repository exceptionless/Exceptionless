using System;
using Foundatio.Repositories.Elasticsearch.Configuration;

namespace Exceptionless.Core.Repositories.Configuration {
    public sealed class OrganizationIndex : VersionedIndex {
        public OrganizationIndex(IElasticConfiguration configuration) : base(configuration, Settings.Current.AppScopePrefix + "organizations", 1) {
            AddType(Application = new ApplicationIndexType(this));
            AddType(Organization = new OrganizationIndexType(this));
            AddType(Project = new ProjectIndexType(this));
            AddType(Token = new TokenIndexType(this));
            AddType(User = new UserIndexType(this));
            AddType(WebHook = new WebHookIndexType(this));
        }
        
        public ApplicationIndexType Application { get; }
        public OrganizationIndexType Organization { get; }
        public ProjectIndexType Project { get; }
        public TokenIndexType Token { get; }
        public UserIndexType User { get; }
        public WebHookIndexType WebHook { get; }
    }
}