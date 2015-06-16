using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : ElasticSearchRepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<Token> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) {}

        public FindResults<Token> GetApiTokens(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.Term(e => e.Type, TokenType.Access) && Filter<Token>.Missing(e => e.UserId);
            return Find(new ElasticSearchOptions<Token>()
                .WithOrganizationId(organizationId)
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("api-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public FindResults<Token> GetByUserId(string userId) {
            var filter = Filter<Token>.Term(e => e.UserId, userId);
            return Find(new ElasticSearchOptions<Token>().WithFilter(filter));
        }

        public FindResults<Token> GetByTypeAndOrganizationId(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find(new ElasticSearchOptions<Token>()
                .WithOrganizationId(organizationId)
                .WithFilter(Filter<Token>.Term(FieldNames.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public FindResults<Token> GetByTypeAndOrganizationIds(TokenType type, ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new FindResults<Token>();

            string cacheKey = String.Concat("type:", type, "-org:", String.Join("", organizationIds).GetHashCode().ToString());
            return Find(new ElasticSearchOptions<Token>()
                .WithOrganizationIds(organizationIds)
                .WithFilter(Filter<Token>.Term(FieldNames.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public FindResults<Token> GetByTypeAndProjectId(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (
                    Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Filter<Token>.Term(t => t.Type, type));

            return Find(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public FindResults<Token> GetByTypeAndOrganizationIdOrProjectId(TokenType type, string organizationId, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (
                    Filter<Token>.Term(t => t.OrganizationId, organizationId) || Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Filter<Token>.Term(t => t.Type, type));

            return Find(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(String.Concat("type:", type, "-org:", organizationId, "-project:", projectId))
                .WithExpiresIn(expiresIn));
        }

        public Token GetByRefreshToken(string refreshToken) {
            if (String.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException("refreshToken");

            return FindOne(new ElasticSearchOptions<Token>().WithFilter(Filter<Token>.Term(FieldNames.Refresh, refreshToken)));
        }

        public override FindResults<Token> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)));

            return Find(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }
        
        private static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string UserId = "uid";
            public const string ApplicationId = "aid";
            public const string DefaultProjectId = "def";
            public const string Refresh = "ref";
            public const string Type = "typ";
            public const string Scopes = "scp";
            public const string ExpiresUtc = "exp";
            public const string Notes = "not";
            public const string CreatedUtc = CommonFieldNames.Date;
            public const string ModifiedUtc = "mdt";
        }
        
        //protected override void ConfigureClassMap(BsonClassMap<Token> cm) {
        //    cm.AutoMap();
        //    cm.SetIgnoreExtraElements(true);
        //    cm.SetIdMember(cm.GetMemberMap(c => c.Id));
        //    cm.GetMemberMap(c => c.OrganizationId).SetElementName(FieldNames.OrganizationId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.UserId).SetElementName(FieldNames.UserId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.ApplicationId).SetElementName(FieldNames.ApplicationId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.DefaultProjectId).SetElementName(FieldNames.DefaultProjectId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.Refresh).SetElementName(FieldNames.Refresh).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.Type).SetElementName(FieldNames.Type);
        //    cm.GetMemberMap(c => c.Scopes).SetElementName(FieldNames.Scopes).SetShouldSerializeMethod(obj => ((Token)obj).Scopes.Any());
        //    cm.GetMemberMap(c => c.ExpiresUtc).SetElementName(FieldNames.ExpiresUtc).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.Notes).SetElementName(FieldNames.Notes).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.CreatedUtc).SetElementName(FieldNames.CreatedUtc);
        //    cm.GetMemberMap(c => c.ModifiedUtc).SetElementName(FieldNames.ModifiedUtc).SetIgnoreIfDefault(true);
        //}

        public override void InvalidateCache(Token token) {
            if (!EnableCache || Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("type:", token.Type, "-org:", token.OrganizationId)));
            Cache.Remove(GetScopedCacheKey(String.Concat("type:", token.Type, "-project:", token.ProjectId ?? token.DefaultProjectId)));
            Cache.Remove(GetScopedCacheKey(String.Concat("type:", token.Type, "-org:", token.OrganizationId, "-project:", token.ProjectId ?? token.DefaultProjectId)));
            base.InvalidateCache(token);
        }
    }
}