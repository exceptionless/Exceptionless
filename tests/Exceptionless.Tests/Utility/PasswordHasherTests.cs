using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class PasswordHasherTests
{
    [Fact]
    public void SetPassword_NewPassword_UsesVersionedHashAndIndependentSalt()
    {
        var first = new User();
        var second = new User();
        first.SetPassword("Pass:word1$");
        second.SetPassword("Pass:word1$");
        Assert.StartsWith("pbkdf2-sha256$600000$", first.Password);
        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Password, second.Password);
        Assert.True(first.IsCorrectPassword("Pass:word1$"));
        Assert.False(first.IsCorrectPassword("wrong"));
        Assert.False(PasswordHasher.NeedsUpgrade(first.Password!));
    }

    [Fact]
    public void Verify_LegacyRecord_PreservesPasswordAndRequiresUpgrade()
    {
        const string salt = "1234567890123456";
        string hash = "Password1$".ToSaltedHash(salt);
        var user = new User { Salt = salt, Password = hash };
        Assert.True(user.IsCorrectPassword("Password1$"));
        Assert.False(user.IsCorrectPassword("wrong"));
        Assert.True(PasswordHasher.NeedsUpgrade(hash));
        Assert.Equal(hash, user.Password);
        Assert.Equal(salt, user.Salt);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", "hash")]
    [InlineData("!", "hash")]
    [InlineData("AA==", "hash")]
    [InlineData("AA==", "pbkdf2-sha256$600000$AA==")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==", "pbkdf2-sha256$2000001$AA==")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==", "pbkdf2-sha256$-1$AA==")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==", "pbkdf2-sha256$0$AA==")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==", "unknown$600000$AA==")]
    public void Verify_MissingOrMalformedRecord_ReturnsFalse(string? salt, string? hash)
        => Assert.False(PasswordHasher.Verify("password", salt, hash));
}
