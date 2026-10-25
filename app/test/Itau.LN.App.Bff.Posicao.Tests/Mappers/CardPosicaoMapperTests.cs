using System.Text.Json;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Mappers;
using Itau.LN.App.Bff.Posicao.Shared.Helpers;

namespace Itau.LN.App.Bff.Posicao.Tests.Mappers;

public class CardPosicaoMapperTests
{
    // JSON literal: pega troca de argumento posicional e mudança de nome/caixa do contrato com o front.
    [Fact]
    public void MapCard_GeraOJsonEsperadoPeloFront()
    {
        var ocorrencia = new EcsHistoricoListaOcorrencia("IDX", "Tipo", "Fechada", 100m, 150m, "2026-10-25", "10:00");
        var detalhe = new EcsHistoricoDetalhe(
            "P1", "termo", 5m, "movimentacoes", 140m, 2m, null,
            [new EcsBlocoOcorrencia("Bloco", 1m, 2m, 3m, 4m)],
            [new EcsCedulaOcorrencia(null, 100.00m, 50m, 1m)],
            [new EcsMoedaOcorrencia("1R", 1m, 30m, 0.5m)]);

        var card = CardPosicaoMapper.MapCard(ocorrencia, detalhe, "OPERADOR");

        Assert.Equal(
            """{"indiceControlePosicao":"IDX","posicao":"P1","tipo":"Tipo","saldo":150,"operador":"OPERADOR","data":"2026-10-25","hora":"10:00","situacao":"Fechada","saldoAnterior":100,"saldoCustodia":[{"bloco":"Bloco","entradasValor":1,"entradasDivergencia":2,"saidasValor":3,"saidasDivergencia":4}],"cedulas":[{"textoCedula":"100.00","valorCedulasBoas":50,"valorDilacerado":0,"valorEntintado":0,"divergencia":1}],"moedas":[{"textoMoeda":"1R","valor":30,"divergencia":0.5}],"resumo":{"saldoAtualMovimentacoes":150,"totalDivergenciasMovimentacoes":5,"totalDivergencia":2,"saldoAtualNaBase":140},"justificativaTermoJuridico":"termo","justificativaDivergenciasMovimentacoes":"movimentacoes","justificativaDivergenciaPadrao":""}""",
            JsonSerializer.Serialize(card, JsonHelper.Options));
    }

    [Fact]
    public void MapCard_SemDadosOpcionaisDoEcs_UsaVazios()
    {
        var ocorrencia = new EcsHistoricoListaOcorrencia("IDX", "Tipo", "Fechada", 100m, 150m, "2026-10-25", "10:00");
        var detalhe = new EcsHistoricoDetalhe(
            "P1", null, 5m, null, 140m, 2m, null, [new EcsBlocoOcorrencia(null, 1m, 2m, 3m, 4m)], null, null);

        var card = CardPosicaoMapper.MapCard(ocorrencia, detalhe, "OPERADOR");

        Assert.Equal("", Assert.Single(card.SaldoCustodia).Bloco);
        Assert.Empty(card.Cedulas);
        Assert.Empty(card.Moedas);
        Assert.Equal("", card.JustificativaTermoJuridico);
        Assert.Equal("", card.JustificativaDivergenciasMovimentacoes);
        Assert.Equal("", card.JustificativaDivergenciaPadrao);
    }
}
