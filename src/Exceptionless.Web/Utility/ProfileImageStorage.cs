using Exceptionless.Core.Utility;
using Foundatio.Storage;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exceptionless.Web.Utility;

internal static class ProfileImageStorage
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    public const long MaxRequestBodySize = MaxFileSize + 64 * 1024;
    public const string MaxFileSizeDisplayName = "5 MB";
    private const string RootPath = "profile-images";

    public static async Task<StoredProfileImage?> SaveAsync(
        IFileStorage storage,
        IFormFile? file,
        string scope,
        string ownerId,
        ModelStateDictionary modelState,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
        {
            modelState.AddModelError("file", "Please select an image file.");
            return null;
        }

        if (file.Length > MaxFileSize)
        {
            modelState.AddModelError("file", $"Image files must be {MaxFileSizeDisplayName} or smaller.");
            return null;
        }

        await using var fileStream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await fileStream.CopyToAsync(buffer, cancellationToken);

        var imageType = DetectImageType(buffer.GetBuffer(), (int)buffer.Length);
        if (imageType is null)
        {
            modelState.AddModelError("file", "Please upload a PNG, JPEG, GIF, or WebP image.");
            return null;
        }

        string fileName = $"{Guid.NewGuid():N}{imageType.Extension}";
        string path = GetStoragePath(scope, ownerId, fileName);

        buffer.Position = 0;
        if (!await storage.SaveFileAsync(path, buffer, cancellationToken))
        {
            modelState.AddModelError("file", "Unable to save the image. Please try again.");
            return null;
        }

        return new StoredProfileImage(fileName, path, imageType.ContentType);
    }

    public static async Task DeleteFromUrlAsync(IFileStorage storage, string? imageUrl, string scope, string ownerId, CancellationToken cancellationToken)
    {
        if (!TryGetStoragePath(imageUrl, scope, ownerId, out string? path))
            return;

        await storage.DeleteFileAsync(path, cancellationToken);
    }

    public static bool TryGetStoragePath(string? imageUrl, string scope, string ownerId, out string path)
    {
        path = String.Empty;

        if (String.IsNullOrWhiteSpace(imageUrl))
            return false;

        string? fileName = imageUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (!IsSafeFileName(fileName))
            return false;

        path = GetStoragePath(scope, ownerId, fileName!);
        return true;
    }

    public static async Task<Stream?> GetFileStreamAsync(IFileStorage storage, string? imageUrl, string scope, string ownerId, CancellationToken cancellationToken)
    {
        if (!TryGetStoragePath(imageUrl, scope, ownerId, out string? path))
            return null;

        return await storage.GetFileStreamAsync(path, StreamMode.Read, cancellationToken);
    }

    public static bool TryGetContentType(string fileName, out string contentType)
    {
        contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".gif" => "image/gif",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => String.Empty
        };

        return contentType.Length > 0;
    }

    private static string GetStoragePath(string scope, string ownerId, string fileName)
        => String.Equals(scope, "organizations", StringComparison.Ordinal)
            ? OrganizationStoragePaths.GetProfileImagePath(ownerId, fileName)
            : $"{RootPath}/{scope}/{ownerId}/{fileName}";

    private static ImageType? DetectImageType(byte[] bytes, int length)
    {
        if (length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A)
            return new ImageType(".png", "image/png");

        if (length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return new ImageType(".jpg", "image/jpeg");

        if (length >= 6 &&
            bytes[0] == 0x47 &&
            bytes[1] == 0x49 &&
            bytes[2] == 0x46 &&
            bytes[3] == 0x38 &&
            (bytes[4] == 0x37 || bytes[4] == 0x39) &&
            bytes[5] == 0x61)
            return new ImageType(".gif", "image/gif");

        if (length >= 12 &&
            bytes[0] == 0x52 &&
            bytes[1] == 0x49 &&
            bytes[2] == 0x46 &&
            bytes[3] == 0x46 &&
            bytes[8] == 0x57 &&
            bytes[9] == 0x45 &&
            bytes[10] == 0x42 &&
            bytes[11] == 0x50)
            return new ImageType(".webp", "image/webp");

        return null;
    }

    private static bool IsSafeFileName(string? fileName)
    {
        if (String.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName || fileName.Contains("..", StringComparison.Ordinal))
            return false;

        if (!TryGetContentType(fileName, out _))
            return false;

        return fileName.All(c => Char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    }

    private sealed record ImageType(string Extension, string ContentType);
}

internal sealed record StoredProfileImage(string FileName, string Path, string ContentType);
