using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Options;

public sealed class ApiOptions
{
    [Required, Url, ConfigurationKeyName("ECS_BASE_URL")]
    public string EcsBaseUrl { get; init; } = string.Empty;

    [Range(1, 300), ConfigurationKeyName("ECS_TIMEOUT_SECONDS")]
    public int EcsTimeoutSeconds { get; init; } = 30;

    [Required, Url, ConfigurationKeyName("CENTRALIZADOR_BASE_URL")]
    public string CentralizadorBaseUrl { get; init; } = string.Empty;

    [Range(1, 300), ConfigurationKeyName("CENTRALIZADOR_TIMEOUT_SECONDS")]
    public int CentralizadorTimeoutSeconds { get; init; } = 30;

    [Required, Url, ConfigurationKeyName("AUTH_TOKEN_URL")]
    public string AuthTokenUrl { get; init; } = string.Empty;
}
