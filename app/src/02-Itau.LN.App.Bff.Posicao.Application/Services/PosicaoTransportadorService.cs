using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Application.Mappers;
using Itau.LN.App.Bff.Posicao.Application.Options;
using Itau.LN.App.Bff.Posicao.Domain.Models;
using Microsoft.Extensions.Options;

namespace Itau.LN.App.Bff.Posicao.Application.Services;

public sealed class PosicaoTransportadorService(IEcsPosicaoClient ecsClient, IOptions<PosicaoOptions> options)
{
    // ponytail: limite fixo de chamadas simultâneas ao ECS; tornar configurável se o ECS limitar a cota.
    private const int MaxChamadasSimultaneas = 4;

    public async Task<IReadOnlyCollection<CardPosicao>> ConsultarCardsAsync(
        string dataMovimento, ApiRequestHeaders headers, CancellationToken cancellationToken)
    {
        var lista = await ecsClient.GetHistoricosListaAsync(dataMovimento, headers, cancellationToken);
        var ocorrencias = lista.Ocorrencias ?? [];
        var cards = new CardPosicao[ocorrencias.Count];

        // Cada card precisa do detalhe; as chamadas rodam em paralelo e o índice preserva a ordem da lista.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, ocorrencias.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxChamadasSimultaneas, CancellationToken = cancellationToken },
            async (indice, token) =>
            {
                var ocorrencia = ocorrencias[indice];
                var detalhe = await ecsClient.GetHistoricoDetalheAsync(ocorrencia.IndiceControlePosicao, headers, token);
                cards[indice] = CardPosicaoMapper.MapCard(ocorrencia, detalhe, options.Value.DefaultOperador);
            });

        return cards;
    }

    public Task ReverterFechamentoAsync(
        string indiceControlePosicao, string codigoTipoPosicao, ApiRequestHeaders headers, CancellationToken cancellationToken) =>
        ecsClient.CancelarPosicaoAsync(
            new EcsCancelarPosicaoRequest(indiceControlePosicao, codigoTipoPosicao), headers, cancellationToken);

    public async Task<JustificativaDivergencias> ConsultarDivergenciasAsync(
        string indiceControlePosicao, ApiRequestHeaders headers, CancellationToken cancellationToken)
    {
        var detalhe = await ecsClient.GetHistoricoDetalheAsync(indiceControlePosicao, headers, cancellationToken);

        return new JustificativaDivergencias(
            indiceControlePosicao,
            detalhe.ValorDivergenciaBloco,
            detalhe.JustificativaDivergenciaBloco ?? string.Empty,
            detalhe.ValorDivergenciaPadrao,
            detalhe.JustificativaDivergenciaPadrao ?? string.Empty);
    }

    public async Task<TermoJuridico> ConsultarTermoJuridicoAsync(
        string indiceControlePosicao, ApiRequestHeaders headers, CancellationToken cancellationToken)
    {
        var detalhe = await ecsClient.GetHistoricoDetalheAsync(indiceControlePosicao, headers, cancellationToken);

        return new TermoJuridico(indiceControlePosicao, detalhe.JustificativaFechamento ?? string.Empty);
    }
}
