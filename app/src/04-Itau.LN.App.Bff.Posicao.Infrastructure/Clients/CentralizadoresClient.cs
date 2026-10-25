using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Centralizadores;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Clients;

public sealed class CentralizadoresClient(HttpClient httpClient, IAuthClient authClient)
    : BaseApiClient(httpClient, authClient), ICentralizadoresClient
{
    public Task<CentralizadorResponse> GetCentralizadorAsync(
        string numeroCentralizador, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        GetAsync<CentralizadorResponse>(Rota(numeroCentralizador), headers, cancellationToken);

    public Task<CentralizadorResponse> GetFornecedorAsync(
        string numeroCentralizador, string fornecedorId, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        GetAsync<CentralizadorResponse>(Rota(numeroCentralizador, "fornecedores", fornecedorId), headers, cancellationToken);

    public Task<CentralizadorResponse> GetFornecedorInBaseAsync(
        string numeroCentralizador, string fornecedorId, string baseId, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        GetAsync<CentralizadorResponse>(
            Rota(numeroCentralizador, "fornecedores", fornecedorId, "bases", baseId), headers, cancellationToken);

    public Task<CentralizadorResponse> GetPoloAsync(
        string numeroCentralizador, string poloId, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        GetAsync<CentralizadorResponse>(Rota(numeroCentralizador, "polos", poloId), headers, cancellationToken);

    // Escapa cada segmento: os valores vêm da rota chamada pelo front.
    private static string Rota(params string[] segments) =>
        "centralizadores/" + string.Join('/', segments.Select(Uri.EscapeDataString));
}
