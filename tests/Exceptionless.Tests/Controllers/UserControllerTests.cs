using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using FluentRest;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class UserControllerTests : IntegrationTestsBase
{
    private readonly IUserRepository _userRepository;

    public UserControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
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
    public Task DeleteCurrentUserAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .Delete()
            .AppendPath("users/me")
            .StatusCodeShouldBeUnauthorized()
        );
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
    public Task VerifyAsync_InvalidToken_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "verify-email-address", "invalidtoken1234567890ab")
            .StatusCodeShouldBeNotFound()
        );
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
        var bytes = new byte[256 * 1024];
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
