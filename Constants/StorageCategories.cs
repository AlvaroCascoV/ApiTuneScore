namespace ApiTuneScore.Constants;

public static class StorageCategories
{
    public const string Albums = "albums";
    public const string Artists = "artists";
    public const string Playlists = "playlists";
    public const string Users = "users";

    public static readonly string[] Allowed =
    [
        Albums,
        Artists,
        Playlists,
        Users
    ];

    public static bool IsValid(string? category)
        => !string.IsNullOrWhiteSpace(category)
           && Allowed.Contains(category.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string category)
        => category.Trim().ToLowerInvariant();

    public static bool BlobMatchesCategory(string category, string blobName)
        => blobName.StartsWith(Normalize(category) + "/", StringComparison.Ordinal);
}

