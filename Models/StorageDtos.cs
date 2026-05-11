namespace ApiTuneScore.Models;

public sealed record UploadBlobResponse(string BlobName);

public sealed record SasUrlResponse(string Url, DateTimeOffset ExpiresAt);

