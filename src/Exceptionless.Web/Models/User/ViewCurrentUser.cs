using System.Security.Cryptography;
using System.Text;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record ViewCurrentUser : ViewUser
{
    public ViewCurrentUser(User user, IntercomOptions options)
    {
        Id = user.Id;
        OrganizationIds = user.OrganizationIds;
        FullName = user.FullName;
        EmailAddress = user.EmailAddress;
        EmailNotificationsEnabled = user.EmailNotificationsEnabled;
        IsEmailAddressVerified = user.IsEmailAddressVerified;
        IsActive = user.IsActive;
        Roles = user.Roles;

        Hash = HMACSHA256HashString(user.Id, options);
        HasLocalAccount = !String.IsNullOrWhiteSpace(user.Password);
        OAuthAccounts = user.OAuthAccounts;
    }

    public string? Hash { get; set; }
    public bool HasLocalAccount { get; set; }
    public ICollection<OAuthAccount> OAuthAccounts { get; set; }

    private static string? HMACSHA256HashString(string value, IntercomOptions options)
    {
        if (!options.EnableIntercom)
            return null;

        byte[] secretKey = Encoding.UTF8.GetBytes(options.IntercomSecret!);
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        using (var hmac = new HMACSHA256(secretKey))
        {
            hmac.ComputeHash(bytes);
            byte[] data = hmac.Hash ?? throw new InvalidOperationException();

            var builder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
                builder.Append(data[i].ToString("x2"));

            return builder.ToString();
        }
    }
}
