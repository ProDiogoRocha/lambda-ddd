using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Centralizadores;

namespace Itau.LN.App.Bff.Posicao.Application.Interfaces;

public interface ICentralizadoresClient
{
    Task<CentralizadorResponse> GetCentralizadorAsync(string numeroCentralizador, ApiRequestHeaders headers, CancellationToken cancellationToken);

    Task<CentralizadorResponse> GetFornecedorAsync(string numeroCentralizador, string fornecedorId, ApiRequestHeaders headers, CancellationToken cancellationToken);

    Task<CentralizadorResponse> GetFornecedorInBaseAsync(string numeroCentralizador, string fornecedorId, string baseId, ApiRequestHeaders headers, CancellationToken cancellationToken);

    Task<CentralizadorResponse> GetPoloAsync(string numeroCentralizador, string poloId, ApiRequestHeaders headers, CancellationToken cancellationToken);
}
