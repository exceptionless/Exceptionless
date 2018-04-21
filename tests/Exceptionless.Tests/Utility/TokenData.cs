using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;

namespace Exceptionless.Tests.Utility {
    internal static class TokenData {
        public static Token GenerateSampleApiKeyToken() {
            return GenerateToken(id: TestConstants.ApiKey, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId);
        }

        public static Token GenerateSampleUserToken() {
            return GenerateToken(id: TestConstants.ApiKey2, userId: TestConstants.UserId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, type: TokenType.Authentication);
        }

        public static Token GenerateToken(bool generateId = false, string id = null, string userId = null, string organizationId = null, string projectId = null, TokenType type = TokenType.Access, string notes = null) {
;            var token = new Token {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : TestConstants.ApiKey : id,
                UserId = userId,
                OrganizationId = organizationId,
                ProjectId = projectId,
                CreatedUtc = SystemClock.UtcNow,
                UpdatedUtc = SystemClock.UtcNow,
                CreatedBy = userId,
                Type = type,
                Notes = notes
            };

            return token;
        }
    }
}