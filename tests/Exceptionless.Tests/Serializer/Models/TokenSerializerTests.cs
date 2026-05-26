using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class TokenSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public TokenSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"650000000000000000000004","organization_id":"550000000000000000000001","project_id":"540000000000000000000001","type":1,"scopes":["client","user"],"notes":"Test token","is_disabled":false,"created_by":"user1","created_utc":"2024-03-15T10:00:00Z","updated_utc":"2024-03-15T10:00:00Z"}""";

        // Act
        var result = _serializer.Deserialize<Token>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("650000000000000000000004", result.Id);
        Assert.Equal("550000000000000000000001", result.OrganizationId);
        Assert.Equal(TokenType.Access, result.Type);
        Assert.Equal(2, result.Scopes.Count);
        Assert.Contains("client", result.Scopes);
        Assert.Contains("user", result.Scopes);
    }

    [Fact]
    public void RoundTrip_WithAccessToken_PreservesValues()
    {
        // Arrange
        var token = new Token
        {
            Id = "650000000000000000000001",
            OrganizationId = "550000000000000000000001",
            ProjectId = "540000000000000000000001",
            Type = TokenType.Access,
            Scopes = ["client"],
            Notes = "Production API key",
            CreatedBy = "user123",
            CreatedUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2024, 6, 1, 8, 30, 0, DateTimeKind.Utc)
        };

        // Act
        string? json = _serializer.SerializeToString(token);
        var result = _serializer.Deserialize<Token>(json);

        // Assert
        SerializerContractAssertions.IncludesProperties(json,
            "organization_id",
            "project_id",
            "created_by",
            "created_utc",
            "updated_utc");
        SerializerContractAssertions.ExcludesProperties(json,
            "OrganizationId",
            "ProjectId",
            "CreatedBy",
            "CreatedUtc",
            "UpdatedUtc");

        Assert.NotNull(result);
        Assert.Equal("650000000000000000000001", result.Id);
        Assert.Equal("550000000000000000000001", result.OrganizationId);
        Assert.Equal("540000000000000000000001", result.ProjectId);
        Assert.Equal(TokenType.Access, result.Type);
        Assert.Contains("client", result.Scopes);
        Assert.Equal("Production API key", result.Notes);
    }

    [Fact]
    public void RoundTrip_WithDisabledSuspended_PreservesFlags()
    {
        // Arrange
        var token = new Token
        {
            Id = "650000000000000000000003",
            OrganizationId = "550000000000000000000001",
            ProjectId = "540000000000000000000001",
            Type = TokenType.Access,
            IsDisabled = true,
            IsSuspended = true,
            CreatedBy = "admin",
            CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        string? json = _serializer.SerializeToString(token);
        var result = _serializer.Deserialize<Token>(json);

        // Assert
        SerializerContractAssertions.IncludesProperties(json, "is_disabled", "is_suspended");
        SerializerContractAssertions.ExcludesProperties(json, "IsDisabled", "IsSuspended");

        Assert.NotNull(result);
        Assert.True(result.IsDisabled);
        Assert.True(result.IsSuspended);
    }

    [Fact]
    public void RoundTrip_WithUserScopedToken_PreservesValues()
    {
        // Arrange
        var token = new Token
        {
            Id = "650000000000000000000002",
            OrganizationId = "",
            ProjectId = "",
            UserId = "660000000000000000000001",
            DefaultProjectId = "540000000000000000000001",
            Type = TokenType.Authentication,
            Refresh = "refresh_token_abc",
            ExpiresUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "system",
            CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        string? json = _serializer.SerializeToString(token);
        var result = _serializer.Deserialize<Token>(json);

        // Assert
        SerializerContractAssertions.IncludesProperties(json, "user_id", "default_project_id", "expires_utc");
        SerializerContractAssertions.ExcludesProperties(json, "UserId", "DefaultProjectId", "ExpiresUtc");

        Assert.NotNull(result);
        Assert.Equal("660000000000000000000001", result.UserId);
        Assert.Equal("540000000000000000000001", result.DefaultProjectId);
        Assert.Equal(TokenType.Authentication, result.Type);
        Assert.Equal("refresh_token_abc", result.Refresh);
        Assert.NotNull(result.ExpiresUtc);
    }
}
