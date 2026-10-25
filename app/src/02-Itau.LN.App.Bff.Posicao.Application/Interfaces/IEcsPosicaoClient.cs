using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;

namespace Itau.LN.App.Bff.Posicao.Application.Interfaces;

public interface IEcsPosicaoClient
{
    Task<EcsHistoricoLista> GetHistoricosListaAsync(string dataMovimento, ApiRequestHeaders headers, CancellationToken cancellationToken);

    Task<EcsHistoricoDetalhe> GetHistoricoDetalheAsync(string indiceControlePosicao, ApiRequestHeaders headers, CancellationToken cancellationToken);

    Task CancelarPosicaoAsync(EcsCancelarPosicaoRequest request, ApiRequestHeaders headers, CancellationToken cancellationToken);
}
