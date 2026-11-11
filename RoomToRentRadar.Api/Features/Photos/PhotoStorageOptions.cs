namespace RoomToRentRadar.Api.Features.Photos;

public sealed class PhotoStorageOptions
{
    public const string ConfigKey = "Storage:PhotoRootPath";
    public const string DefaultRelativeRootPath = "App_Data/media";

    private PhotoStorageOptions(string rootPath) => RootPath = rootPath;

    public string RootPath { get; }

    public static PhotoStorageOptions Resolve(string? configuredRootPath, string contentRootPath)
    {
        string rootPath;
        if (string.IsNullOrWhiteSpace(configuredRootPath))
        {
            rootPath = Path.Combine(contentRootPath, DefaultRelativeRootPath);
        }
        else if (Path.IsPathRooted(configuredRootPath))
        {
            rootPath = configuredRootPath;
        }
        else
        {
            rootPath = Path.Combine(contentRootPath, configuredRootPath);
        }

        rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(rootPath);
        return new PhotoStorageOptions(rootPath);
    }
}
