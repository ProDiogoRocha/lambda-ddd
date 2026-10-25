using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Clients;

public sealed class EcsPosicaoClient(HttpClient httpClient, IAuthClient authClient)
    : BaseApiClient(httpClient, authClient), IEcsPosicaoClient
{
    private const string BasePath = "posicaonumerarionacional/v2";

    public Task<EcsHistoricoLista> GetHistoricosListaAsync(
        string dataMovimento, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        GetAsync<EcsHistoricoLista>(
            $"{BasePath}/custodias-posicoes-historicos-listas-consultar?dataMovimento={Uri.EscapeDataString(dataMovimento)}",
            headers,
            cancellationToken);

    public Task<EcsHistoricoDetalhe> GetHistoricoDetalheAsync(
        string indiceControlePosicao, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        GetAsync<EcsHistoricoDetalhe>(
            $"{BasePath}/custodias-posicoes-historicos-detalhes-consultar?indiceControlePosicao={Uri.EscapeDataString(indiceControlePosicao)}",
            headers,
            cancellationToken);

    public Task CancelarPosicaoAsync(
        EcsCancelarPosicaoRequest request, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        PostAsync($"{BasePath}/custodia-posicoes-cancelamento", request, headers, cancellationToken);
}
