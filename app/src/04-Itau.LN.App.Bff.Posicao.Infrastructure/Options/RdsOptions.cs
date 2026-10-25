using System.ComponentModel.DataAnnotations;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Options;

// Seção "RdsConfig" (env vars RdsConfig__UserSecret, RdsConfig__PasswordSecret, ...).
public sealed class RdsOptions
{
    [Required]
    public string UserSecret { get; init; } = string.Empty;

    [Required]
    public string PasswordSecret { get; init; } = string.Empty;

    [Required]
    public string EndpointSecret { get; init; } = string.Empty;

    [Required]
    public string Database { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 3306;
}
