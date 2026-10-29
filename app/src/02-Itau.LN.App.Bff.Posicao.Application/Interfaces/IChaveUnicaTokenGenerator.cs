namespace Itau.LN.App.Bff.Posicao.Application.Interfaces;

// Gera o valor do header chave-unica enviado ao ECS.
public interface IChaveUnicaTokenGenerator
{
    Task<string> GerarAsync(string codigoTransportadora, string codigoFilial, CancellationToken cancellationToken);
}
