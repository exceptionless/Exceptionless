using System.Net;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using FluentRest;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class UserEndpointTests : IntegrationTestsBase
{
    private readonly IUserRepository _userRepository;

    public UserEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _userRepository = GetService<IUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task AddAdminRoleAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var user = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AppendPaths("users", user.Id, "admin-role")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task AddAdminRoleAsync_AsGlobalAdmin_AddsRole()
    {
        // Arrange
        var user = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", user.Id, "admin-role")
            .StatusCodeShouldBeOk()
        );

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Contains(AuthorizationRoles.GlobalAdmin, updatedUser.Roles);
    }

    [Fact]
    public async Task AddAdminRoleAsync_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var user = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("users", user.Id, "admin-role")
            .StatusCodeShouldBeForbidden()
        );

        // Assert - role was not added
        var unchanged = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(unchanged);
        Assert.DoesNotContain(AuthorizationRoles.GlobalAdmin, unchanged.Roles);
    }

    [Fact]
    public async Task DeleteAdminRoleAsync_AsGlobalAdmin_RemovesRole()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "admin-role")
            .StatusCodeShouldBeNoContent()
        );

        // Assert
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        Assert.DoesNotContain(AuthorizationRoles.GlobalAdmin, user.Roles);
    }

    [Fact]
    public async Task DeleteAdminRoleAsync_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("users", currentUser.Id, "admin-role")
            .StatusCodeShouldBeForbidden()
        );

        // Assert - role was not removed
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        Assert.Contains(AuthorizationRoles.GlobalAdmin, user.Roles);
    }

    [Fact]
    public async Task DeleteAsync_AsGlobalAdmin_ReturnsAccepted()
    {
        // Arrange
        var user = new User
        {
            FullName = "Deletable User",
            EmailAddress = "deletable@exceptionless.test",
            IsEmailAddressVerified = true
        };
        user.Roles.Add(AuthorizationRoles.Client);
        user.Roles.Add(AuthorizationRoles.User);
        user = await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

        // Act
        var response = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("users", user.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Assert
        Assert.NotNull(response);
    }

    [Fact]
    public async Task DeleteAsync_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var user = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("users", user.Id)
            .StatusCodeShouldBeForbidden()
        );

        // Assert - user still exists
        var unchanged = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(unchanged);
    }

    [Fact]
    public async Task DeleteAvatarAsync_WithExistingAvatar_RemovesAvatar()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);
        using var content = CreateProfileImageContent();

        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "avatar")
            .Content(content)
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.AvatarUrl);

        // Act
        var userWithoutAvatar = await SendRequestAsAsync<ViewUser>(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "avatar")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(userWithoutAvatar);
        Assert.Null(userWithoutAvatar.AvatarUrl);

        var storedUser = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(storedUser);
        Assert.Null(storedUser.AvatarFileName);
    }

    [Fact]
    public Task DeleteCurrentUserAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .Delete()
            .AppendPath("users/me")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task DeleteCurrentUserAsync_WithOrganizationMembership_ReturnsBadRequest()
    {
        // Arrange
        var currentUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(currentUser);
        Assert.NotEmpty(currentUser.OrganizationIds);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        var storedUser = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(storedUser);
        Assert.Contains(SampleDataService.TEST_ORG_ID, storedUser.OrganizationIds);
    }

    [Fact]
    public async Task GetAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .AppendPaths("users", currentUser.Id)
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public Task GetAsync_InvalidId_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "000000000000000000000000")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetAsync_ValidId_ReturnsUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(currentUser.Id, user.Id);
        Assert.Equal(SampleDataService.TEST_USER_EMAIL, user.EmailAddress);
    }

    [Fact]
    public async Task GetAvatarAsync_WithExistingAvatar_ReturnsImage()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);
        using var content = CreateProfileImageContent();

        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "avatar")
            .Content(content)
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.AvatarUrl);
        string avatarPath = updatedUser.AvatarUrl.TrimStart('/');

        // Act
        var response = await SendRequestAsync(r => r
            .BaseUri(_server.BaseAddress)
            .AppendPath(avatarPath)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
    }

    [Fact]
    public Task GetByOrganizationAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AppendPath($"organizations/{SampleDataService.TEST_ORG_ID}/users")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task GetByOrganizationAsync_ValidOrganization_ReturnsUsers()
    {
        // Act
        var users = await SendRequestAsAsync<IReadOnlyCollection<ViewUser>>(r => r
            .AsGlobalAdminUser()
            .AppendPath($"organizations/{SampleDataService.TEST_ORG_ID}/users")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(users);
        Assert.NotEmpty(users);
    }

    [Fact]
    public Task GetCurrentUserAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AppendPath("users/me")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task GetCurrentUserAsync_AuthenticatedUser_ReturnsCurrentUser()
    {
        // Act
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(SampleDataService.TEST_USER_EMAIL, user.EmailAddress);
        Assert.NotNull(user.Id);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task GetCurrentUserAsync_TestOrganizationUser_ReturnsCurrentUser()
    {
        // Act
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(SampleDataService.TEST_ORG_USER_EMAIL, user.EmailAddress);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithAvatar_ReturnsRoutableAvatarUrl()
    {
        // Arrange
        var currentUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(currentUser);
        currentUser.AvatarFileName = "avatar.png";
        await _userRepository.SaveAsync(currentUser, o => o.ImmediateConsistency().Cache());

        // Act
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal($"/api/v2/users/{currentUser.Id}/avatar/avatar.png", user.AvatarUrl);
    }


    [Fact]
    public async Task UploadAvatarAsync_ImageOverGlobalRequestLimit_ReturnsUpdatedUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);
        using var content = CreateProfileImageContent();

        // Act
        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "avatar")
            .Content(content)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedUser);
        Assert.Contains($"/users/{currentUser.Id}/avatar/", updatedUser.AvatarUrl);

        var storedUser = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(storedUser);
        Assert.Equal(updatedUser.AvatarUrl?.Split('/').Last(), storedUser.AvatarFileName);
        Assert.DoesNotContain("/", storedUser.AvatarFileName!);
    }

    [Fact]
    public async Task UploadAvatarAsync_NonExistentUser_ReturnsNotFoundBeforeFileValidation()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("ignored"), "description");

        // Act
        using var response = await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", "000000000000000000000000", "avatar")
            .Content(content)
            .StatusCodeShouldBeNotFound()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .Patch()
            .AppendPaths("users", currentUser.Id)
            .Content(new { FullName = "Hacker" })
            .StatusCodeShouldBeUnauthorized()
        );

        // Assert - name was not changed
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        Assert.NotEqual("Hacker", user.FullName);
    }

    [Fact]
    public async Task PatchAsync_UpdateFullName_ReturnsUpdatedUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id)
            .Content(new { FullName = "Updated Name" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedUser);
        Assert.Equal("Updated Name", updatedUser.FullName);
    }

    [Fact]
    public async Task PatchAsync_UpdateNotifications_ReturnsUpdatedUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id)
            .Content(new { EmailNotificationsEnabled = false })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser.EmailNotificationsEnabled);
    }

    [Fact]
    public Task PatchAsync_WithNonExistentId_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("users", "000000000000000000000000")
            .Content(new { FullName = "Nobody" })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PutAsync_UpdateFullName_ReturnsUpdatedUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id)
            .Content(new { FullName = "Put Updated Name" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedUser);
        Assert.Equal("Put Updated Name", updatedUser.FullName);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .AppendPaths("users", currentUser.Id, "resend-verification-email")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_ValidUser_ReturnsOk()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "resend-verification-email")
            .StatusCodeShouldBeOk()
        );
    }

    [Fact]
    public Task UnverifyEmailAddressAsync_AsGlobalAdmin_ReturnsOk()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("users/unverify-email-address")
            .Content(SampleDataService.TEST_USER_EMAIL, "text/plain")
            .StatusCodeShouldBeOk()
        );
    }

    [Fact]
    public Task UnverifyEmailAddressAsync_NonAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("users/unverify-email-address")
            .Content(SampleDataService.TEST_USER_EMAIL, "text/plain")
            .StatusCodeShouldBeForbidden()
        );
    }

    [Fact]
    public async Task UnverifyEmailAddressAsync_NonTextBody_ReturnsUnsupportedMediaType()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        user.MarkEmailAddressVerified();
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency());

        // Act
        using var response = await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("users/unverify-email-address")
            .Content($"\"{SampleDataService.TEST_USER_EMAIL}\"", "application/json")
            .ExpectedStatus(HttpStatusCode.UnsupportedMediaType)
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var unchangedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(unchangedUser);
        Assert.True(unchangedUser.IsEmailAddressVerified);
    }

    [Fact]
    public async Task UpdateEmailAddressAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AppendPaths("users", currentUser.Id, "email-address", "newemail@exceptionless.test")
            .StatusCodeShouldBeUnauthorized()
        );

        // Assert - email was not changed
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        Assert.NotEqual("newemail@exceptionless.test", user.EmailAddress);
    }

    [Fact]
    public async Task UpdateEmailAddressAsync_ValidEmail_ReturnsResult()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var result = await SendRequestAsAsync<UpdateEmailAddressResult>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "email-address", "newemail@exceptionless.test")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task VerifyAsync_ExpiredToken_ReturnsValidationProblem()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        user.VerifyEmailAddressTokenExpiration = TimeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());
        string token = Assert.IsType<string>(user.VerifyEmailAddressToken);

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "verify-email-address", token)
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser.IsEmailAddressVerified);
    }

    [Fact]
    public async Task VerifyAsync_InvalidToken_ReturnsNotFound()
    {
        // Arrange
        const string token = "invalidtoken1234567890ab";

        // Act
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "verify-email-address", token)
            .StatusCodeShouldBeNotFound()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_ValidToken_VerifiesEmailAddress()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());
        string token = Assert.IsType<string>(user.VerifyEmailAddressToken);

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "verify-email-address", token)
            .StatusCodeShouldBeOk()
        );

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.IsEmailAddressVerified);
        Assert.Null(updatedUser.VerifyEmailAddressToken);
    }


    private async Task<ViewUser> GetTestOrganizationUserAsync()
    {
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(user);
        return user;
    }

    private static MultipartFormDataContent CreateProfileImageContent()
    {
        byte[] bytes = new byte[256 * 1024];
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Array.Copy(pngHeader, bytes, pngHeader.Length);
        Assert.True(bytes.Length < ProfileImageStorage.MaxFileSize);

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new("image/png");

        var content = new MultipartFormDataContent();
        content.Add(fileContent, "file", "avatar.png");
        return content;
    }
}
