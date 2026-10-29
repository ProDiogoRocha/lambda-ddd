using System.ComponentModel.DataAnnotations;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Options;

// Seção "Secrets" (env vars Secrets__ConsumoApi e Secrets__ChaveUnicaJwt).
public sealed class SecretsOptions
{
    [Required]
    public string ConsumoApi { get; init; } = string.Empty;

    // Sem [Required]: só as rotas de posição assinam a chave-unica; sem ela os centralizadores seguem funcionando.
    public string ChaveUnicaJwt { get; init; } = string.Empty;
}
