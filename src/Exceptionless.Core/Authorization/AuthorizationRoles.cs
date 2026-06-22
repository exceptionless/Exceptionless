namespace Exceptionless.Core.Authorization;

public static class AuthorizationRoles
{
    public const string ClientPolicy = nameof(ClientPolicy);
    public const string Client = "client";
    public const string UserPolicy = nameof(UserPolicy);
    public const string User = "user";
    public const string GlobalAdminPolicy = nameof(GlobalAdminPolicy);
    public const string GlobalAdmin = "global";
    public const string McpPolicy = nameof(McpPolicy);
    public const string McpRead = "mcp:read";
    public const string ProjectsRead = "projects:read";
    public const string StacksRead = "stacks:read";
    public const string EventsRead = "events:read";
    public const string OfflineAccess = "offline_access";
    public static readonly ISet<string> AllScopes = new HashSet<string>([Client, User, GlobalAdmin]);
}
