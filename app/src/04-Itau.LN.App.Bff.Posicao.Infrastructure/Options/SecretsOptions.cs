using System.ComponentModel.DataAnnotations;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Options;

// Seção "Secrets" (env var Secrets__ConsumoApi).
public sealed class SecretsOptions
{
    [Required]
    public string ConsumoApi { get; init; } = string.Empty;
}
