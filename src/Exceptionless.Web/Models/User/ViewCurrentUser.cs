using System.Security.Cryptography;
using System.Text;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Web.Models;

public record ViewCurrentUser : ViewUser
{
    public ViewCurrentUser(User user, IntercomOptions options)
    {
        Id = user.Id;
        OrganizationIds = user.OrganizationIds;
        FullName = user.FullName;
        EmailAddress = user.EmailAddress;
        AvatarUrl = user.AvatarFileName;
        EmailNotificationsEnabled = user.EmailNotificationsEnabled;
        IsEmailAddressVerified = user.IsEmailAddressVerified;
        IsActive = user.IsActive;
        Roles = user.Roles;
        OrganizationPreferences = user.OrganizationPreferences;
        SavedViewOrders = user.SavedViewOrders;

        Hash = HMACSHA256HashString(user.Id, options);
        HasLocalAccount = !String.IsNullOrWhiteSpace(user.Password);
        OAuthAccounts = user.OAuthAccounts;
        ProductTours = new Dictionary<string, ProductTourProgress>(user.ProductTours, StringComparer.Ordinal);
    }

    public string? Hash { get; set; }
    public bool HasLocalAccount { get; set; }
    public ICollection<OAuthAccount> OAuthAccounts { get; set; }
    public ICollection<UserOrganizationPreference> OrganizationPreferences { get; set; }
    public ICollection<UserSavedViewOrderPreference> SavedViewOrders { get; set; }
    public IDictionary<string, ProductTourProgress> ProductTours { get; set; } = new Dictionary<string, ProductTourProgress>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, int> ProductTourVersions { get; } = Exceptionless.Core.Models.Data.ProductTours.Versions;

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
