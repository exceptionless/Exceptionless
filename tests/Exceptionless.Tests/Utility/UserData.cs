using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Tests.Utility;

public class UserData
{
    private readonly TimeProvider _timeProvider;

    public UserData(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IEnumerable<User> GenerateUsers(int count = 10, bool generateId = false, string? id = null, string? organizationId = null, string? emailAddress = null, List<string>? roles = null)
    {
        for (int i = 0; i < count; i++)
            yield return GenerateUser(generateId, id, organizationId, emailAddress, roles);
    }

    public IEnumerable<User> GenerateSampleUsers()
    {
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

    public User GenerateSampleUser()
    {
        return GenerateUser(id: TestConstants.UserId, organizationId: TestConstants.OrganizationId, emailAddress: TestConstants.UserEmail, roles: new List<string> {
                AuthorizationRoles.GlobalAdmin,
                AuthorizationRoles.User,
                AuthorizationRoles.Client
            });
    }

    public User GenerateSampleUserWithNoRoles()
    {
        return GenerateUser(id: TestConstants.UserIdWithNoRoles, organizationId: TestConstants.OrganizationId, emailAddress: TestConstants.UserEmailWithNoRoles);
    }

    public User GenerateUser(bool generateId = false, string? id = null, string? organizationId = null, string? emailAddress = null, IEnumerable<string>? roles = null)
    {
        var user = new User
        {
            Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : TestConstants.UserId : id,
            EmailAddress = emailAddress.IsNullOrEmpty() ? String.Concat(RandomData.GetWord(false), "@", RandomData.GetWord(false), ".com") : emailAddress,
            Password = TestConstants.UserPassword,
            FullName = "Eric Smith"
        };

        user.CreatePasswordResetToken(_timeProvider);
        user.OrganizationIds.Add(organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId);

        if (roles is not null)
            user.Roles.AddRange(roles);

        return user;
    }
}
