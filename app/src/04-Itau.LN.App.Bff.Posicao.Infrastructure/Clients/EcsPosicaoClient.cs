using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Clients;

// Endpoints relativos à ECS_BASE_URL, que já termina em /posicaonumerarionacional/v2.
public sealed class EcsPosicaoClient(HttpClient httpClient, IAuthClient authClient)
    : BaseApiClient(httpClient, authClient), IEcsPosicaoClient
{
    public Task<EcsHistoricoLista> GetHistoricosListaAsync(
        string dataMovimento, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        GetAsync<EcsHistoricoLista>(
            $"custodias-posicoes-historicos-listas-consultar?dataMovimento={Uri.EscapeDataString(dataMovimento)}",
            headers,
            cancellationToken);

    public Task<EcsHistoricoDetalhe> GetHistoricoDetalheAsync(
        string indiceControlePosicao, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        GetAsync<EcsHistoricoDetalhe>(
            $"custodias-posicoes-historicos-detalhes-consultar?indiceControlePosicao={Uri.EscapeDataString(indiceControlePosicao)}",
            headers,
            cancellationToken);

    public Task CancelarPosicaoAsync(
        EcsCancelarPosicaoRequest request, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        PostAsync("custodia-posicoes-cancelamento", request, headers, cancellationToken);
}
