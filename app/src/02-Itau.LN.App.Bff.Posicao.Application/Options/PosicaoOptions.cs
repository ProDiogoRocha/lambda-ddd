using Microsoft.Extensions.Configuration;

namespace Itau.LN.App.Bff.Posicao.Application.Options;

public sealed class PosicaoOptions
{
    [ConfigurationKeyName("BFF_DEFAULT_OPERADOR")]
    public string DefaultOperador { get; init; } = "NA";
}
