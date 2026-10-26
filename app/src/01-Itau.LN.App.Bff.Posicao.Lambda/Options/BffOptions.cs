using Microsoft.Extensions.Configuration;

namespace Itau.LN.App.Bff.Posicao.Lambda.Options;

public sealed class BffOptions
{
    [ConfigurationKeyName("API_BASE_PATH")]
    public string ApiBasePath { get; init; } = "/v1";

    // Origem do front no CORS; a mesma do cors_allowed_origin do API Gateway. Vazia: sem header CORS.
    [ConfigurationKeyName("CORS_ALLOWED_ORIGIN")]
    public string? CorsAllowedOrigin { get; init; }
}
