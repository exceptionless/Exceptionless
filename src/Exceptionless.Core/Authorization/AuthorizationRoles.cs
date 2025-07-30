namespace Exceptionless.Core.Authorization;

public static class AuthorizationRoles
{
    public const string ClientPolicy = nameof(ClientPolicy);
    public const string Client = "client";
    public const string UserPolicy = nameof(UserPolicy);
    public const string User = "user";
    public const string GlobalAdminPolicy = nameof(GlobalAdminPolicy);
    public const string GlobalAdmin = "global";
    public static readonly ISet<string> AllScopes = new HashSet<string>([Client, User, GlobalAdmin]);
}
