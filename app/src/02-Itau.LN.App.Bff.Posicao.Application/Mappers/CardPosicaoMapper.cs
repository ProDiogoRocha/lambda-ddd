using System.Globalization;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Domain.Models;

namespace Itau.LN.App.Bff.Posicao.Application.Mappers;

public static class CardPosicaoMapper
{
    public static CardPosicao MapCard(EcsHistoricoListaOcorrencia ocorrencia, EcsHistoricoDetalhe detalhe, string operador)
    {
        var saldoCustodia = (detalhe.BlocoOcorrenciaLista ?? [])
            .Select(item => new SaldoCustodia(
                item.DescricaoItemBloco ?? string.Empty,
                item.ValorEntradaItemBloco,
                item.ValorDivergenciaEntradaBloco,
                item.ValorSaidaItemBloco,
                item.ValorDivergenciaSaidaBloco))
            .ToArray();

        var cedulas = (detalhe.CedulasOcorrenciaLista ?? [])
            .Select(item => new Cedula(
                TextoOuValorFace(item.CodigoClassificacaoCedula, item.ValorFaceCedula),
                item.ValorInformadoCedula,
                ValorDilacerado: 0,
                ValorEntintado: 0,
                item.ValorDivergenciaItemCedula))
            .ToArray();

        var moedas = (detalhe.MoedasOcorrenciaLista ?? [])
            .Select(item => new Moeda(
                TextoOuValorFace(item.CodigoClassificacaoMoeda, item.ValorFaceMoeda),
                item.ValorInformadoMoeda,
                item.ValorDivergenciaItemMoeda))
            .ToArray();

        return new CardPosicao(
            ocorrencia.IndiceControlePosicao,
            detalhe.NumeroPosicao,
            ocorrencia.DescricaoPosicao,
            ocorrencia.ValorSaldoAtualComposicao,
            operador,
            ocorrencia.DataCadastro,
            ocorrencia.HoraCadastro,
            ocorrencia.DescricaoSituacaoPosicao,
            ocorrencia.ValorSaldoAnteriorComposicao,
            saldoCustodia,
            cedulas,
            moedas,
            new Resumo(
                ocorrencia.ValorSaldoAtualComposicao,
                detalhe.ValorDivergenciaBloco,
                detalhe.ValorDivergenciaPadrao,
                detalhe.ValorSaldoAtualPadrao),
            detalhe.JustificativaFechamento ?? string.Empty,
            detalhe.JustificativaDivergenciaBloco ?? string.Empty,
            detalhe.JustificativaDivergenciaPadrao ?? string.Empty);
    }

    private static string TextoOuValorFace(string? codigoClassificacao, decimal valorFace) =>
        string.IsNullOrWhiteSpace(codigoClassificacao)
            ? valorFace.ToString(CultureInfo.InvariantCulture)
            : codigoClassificacao;
}
