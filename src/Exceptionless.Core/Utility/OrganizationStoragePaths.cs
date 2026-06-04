namespace Exceptionless.Core.Utility;

public static class OrganizationStoragePaths
{
    public static string GetRootPath(string organizationId)
        => $"organizations/{organizationId}";

    public static string GetProfileImagesPath(string organizationId)
        => $"{GetRootPath(organizationId)}/profile-images";

    public static string GetProfileImagePath(string organizationId, string fileName)
        => $"{GetProfileImagesPath(organizationId)}/{fileName}";

    public static string GetLegacyProfileImagesPath(string organizationId)
        => $"profile-images/organizations/{organizationId}";

    public static string GetLegacyProfileImagePath(string organizationId, string fileName)
        => $"{GetLegacyProfileImagesPath(organizationId)}/{fileName}";
}
