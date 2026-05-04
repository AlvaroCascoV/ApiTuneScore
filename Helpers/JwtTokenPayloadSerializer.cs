using System.Text.Json;

namespace ApiTuneScore.Helpers;

internal static class JwtTokenPayloadSerializer
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
