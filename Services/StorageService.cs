using System.Text;
using ApiTuneScore.Constants;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ApiTuneScore.Services;

public sealed class StorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly BlobContainerClient _container;
    private readonly StorageSharedKeyCredential _sharedKeyCredential;
    private readonly int _readSasMinutes;
    private readonly long _maxUploadBytes;
    private readonly string _containerName;

    public StorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureKeys:StorageAccount"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing AzureKeys:StorageAccount (set via Key Vault secret AzureKeys--StorageAccount or App Service setting AzureKeys__StorageAccount).");

        var containerName = configuration["Storage:ContainerName"];
        if (string.IsNullOrWhiteSpace(containerName))
            throw new InvalidOperationException("Missing Storage:ContainerName.");
        _containerName = containerName.Trim();

        _readSasMinutes = configuration.GetValue("Storage:ReadSasMinutes", 15);
        _maxUploadBytes = configuration.GetValue("Storage:MaxUploadBytes", 5L * 1024 * 1024);

        var (accountName, accountKey) = ParseAccountNameAndKey(connectionString);
        _sharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);

        var serviceClient = new BlobServiceClient(connectionString);
        _container = serviceClient.GetBlobContainerClient(_containerName);
    }

    public async Task<string> UploadAsync(string category, IFormFile file, bool keepName = false, CancellationToken cancellationToken = default)
    {
        if (!StorageCategories.IsValid(category))
            throw new ArgumentException("Invalid category.", nameof(category));

        if (file == null)
            throw new ArgumentNullException(nameof(file));

        if (file.Length <= 0)
            throw new ArgumentException("File is empty.", nameof(file));

        if (file.Length > _maxUploadBytes)
            throw new InvalidOperationException($"File exceeds the maximum allowed size ({_maxUploadBytes} bytes).");

        var contentType = (file.ContentType ?? string.Empty).Trim();
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only image uploads are allowed.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Unsupported image extension. Allowed: .jpg, .jpeg, .png, .webp.");

        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var normalizedCategory = StorageCategories.Normalize(category);
        var blobName = keepName
            ? $"{normalizedCategory}/{SanitizeFileName(Path.GetFileNameWithoutExtension(file.FileName))}{extension.ToLowerInvariant()}"
            : $"{normalizedCategory}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        var blobClient = _container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            },
            cancellationToken);

        return blobName;
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("Blob name is required.", nameof(blobName));

        var blobClient = _container.GetBlobClient(blobName.Trim());
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public (string url, DateTimeOffset expiresAt) GenerateReadSas(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("Blob name is required.", nameof(blobName));

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_readSasMinutes);

        var builder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobName.Trim(),
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = expiresAt
        };
        builder.SetPermissions(BlobSasPermissions.Read);
        var sas = builder.ToSasQueryParameters(_sharedKeyCredential);

        var blobClient = _container.GetBlobClient(blobName.Trim());
        var uriBuilder = new BlobUriBuilder(blobClient.Uri)
        {
            Sas = sas
        };

        return (uriBuilder.ToUri().ToString(), expiresAt);
    }

    private static string SanitizeFileName(string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            return "file";

        var sb = new StringBuilder(fileNameWithoutExtension.Length);
        foreach (var c in fileNameWithoutExtension.Trim())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
                sb.Append(c);
            else
                sb.Append('-');
        }

        var sanitized = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }

    private static (string accountName, string accountKey) ParseAccountNameAndKey(string connectionString)
    {
        string? name = null;
        string? key = null;

        foreach (var rawPart in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Trim();
            var idx = part.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0) continue;

            var k = part[..idx].Trim();
            var v = part[(idx + 1)..].Trim();

            if (k.Equals("AccountName", StringComparison.OrdinalIgnoreCase))
                name = v;
            else if (k.Equals("AccountKey", StringComparison.OrdinalIgnoreCase))
                key = v;
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("AzureKeys:StorageAccount must include AccountName and AccountKey.");

        return (name, key);
    }
}

