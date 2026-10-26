namespace Itau.LN.App.Bff.Posicao.Application.Interfaces;

public interface ICredenciaisRepository
{
    Task<string?> ObterChaveUnicaAsync(string codigoTransportadora, string codigoBase, CancellationToken cancellationToken);
}
