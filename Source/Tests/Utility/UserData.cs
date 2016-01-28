using System;
using System.Collections.Generic;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Tests.Utility {
    internal static class UserData {
        public static IEnumerable<User> GenerateUsers(int count = 10, bool generateId = false, string id = null, string organizationId = null, string emailAddress = null, List<string> roles = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateUser(generateId, id, organizationId, emailAddress, roles);
        }

        public static IEnumerable<User> GenerateSampleUsers() {
            return new List<User> {
                GenerateSampleUser(),
                GenerateSampleUserWithNoRoles(),
                GenerateUser(id: TestConstants.UserId2, organizationId: TestConstants.OrganizationId2, emailAddress: TestConstants.UserEmail2, roles: new List<string> {
                    AuthorizationRoles.GlobalAdmin,
                    AuthorizationRoles.User,
                    AuthorizationRoles.Client
                })
            };
        }

        public static User GenerateSampleUser() {
            return GenerateUser(id: TestConstants.UserId, organizationId: TestConstants.OrganizationId, emailAddress: TestConstants.UserEmail, roles: new List<string> {
                AuthorizationRoles.GlobalAdmin,
                AuthorizationRoles.User,
                AuthorizationRoles.Client
            });
        }

        public static User GenerateSampleUserWithNoRoles() {
            return GenerateUser(id: TestConstants.UserIdWithNoRoles, organizationId: TestConstants.OrganizationId, emailAddress: TestConstants.UserEmailWithNoRoles);
        }

        public static User GenerateUser(bool generateId = false, string id = null, string organizationId = null, string emailAddress = null, IEnumerable<string> roles = null) {
            var user = new User {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : TestConstants.UserId : id,
                EmailAddress = emailAddress.IsNullOrEmpty() ? String.Concat(RandomData.GetWord(false), "@", RandomData.GetWord(false), ".com") : emailAddress,
                Password = TestConstants.UserPassword,
                FullName = "Eric Smith",
                PasswordResetToken = Guid.NewGuid().ToString()
            };

            user.OrganizationIds.Add(organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId);

            if (roles != null)
                user.Roles.AddRange(roles);

            return user;
        }
    }
}