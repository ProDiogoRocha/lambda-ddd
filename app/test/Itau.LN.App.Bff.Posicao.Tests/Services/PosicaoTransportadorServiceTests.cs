using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Application.Options;
using Itau.LN.App.Bff.Posicao.Application.Services;
using Itau.LN.App.Bff.Posicao.Domain.Models;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Services;

// Ordem dos cards e erro em um detalhe são cobertos pelos testes do BffRequestHandler, que usa este serviço real.
public class PosicaoTransportadorServiceTests
{
    private static readonly ApiRequestHeaders Headers = new("api-key", "corr-id", "chave");

    private readonly IEcsPosicaoClient _ecs = Substitute.For<IEcsPosicaoClient>();
    private readonly CancellationToken _token = new CancellationTokenSource().Token;
    private readonly PosicaoTransportadorService _service;

    public PosicaoTransportadorServiceTests() =>
        _service = new PosicaoTransportadorService(_ecs, Options.Create(new PosicaoOptions()));

    [Fact]
    public async Task ConsultarCards_UsaOOperadorConfigurado()
    {
        _ecs.GetHistoricosListaAsync("2026-10-25", Headers, _token)
            .Returns(new EcsHistoricoLista([new EcsHistoricoListaOcorrencia("IDX", "Tipo", "Fechada", 1m, 2m, "2026-10-25", "10:00")]));
        // Os detalhes recebem um token derivado do Parallel.ForEachAsync, não o original.
        _ecs.GetHistoricoDetalheAsync("IDX", Headers, Arg.Any<CancellationToken>())
            .Returns(new EcsHistoricoDetalhe("P1", null, 5m, null, 140m, 2m, null, null, null, null));
        var service = new PosicaoTransportadorService(_ecs, Options.Create(new PosicaoOptions { DefaultOperador = "OP" }));

        var cards = await service.ConsultarCardsAsync("2026-10-25", Headers, _token);

        Assert.Equal("OP", Assert.Single(cards).Operador);
    }

    [Fact]
    public async Task ConsultarDivergencias_MapeiaTotaisEJustificativas()
    {
        Detalhe(justificativaBloco: "bloco", justificativaPadrao: "padrao");

        var divergencias = await _service.ConsultarDivergenciasAsync("IDX", Headers, _token);

        Assert.Equal(new JustificativaDivergencias("IDX", 5m, "bloco", 2m, "padrao"), divergencias);
    }

    [Fact]
    public async Task ConsultarDivergencias_SemJustificativas_UsaTextoVazio()
    {
        Detalhe();

        var divergencias = await _service.ConsultarDivergenciasAsync("IDX", Headers, _token);

        Assert.Equal(new JustificativaDivergencias("IDX", 5m, "", 2m, ""), divergencias);
    }

    [Theory]
    [InlineData("termo", "termo")]
    [InlineData(null, "")]
    public async Task ConsultarTermoJuridico_UsaJustificativaDeFechamento(string? justificativa, string esperado)
    {
        Detalhe(justificativaFechamento: justificativa);

        var termo = await _service.ConsultarTermoJuridicoAsync("IDX", Headers, _token);

        Assert.Equal(new TermoJuridico("IDX", esperado), termo);
    }

    [Fact]
    public async Task ReverterFechamento_CancelaAPosicaoNoEcs()
    {
        await _service.ReverterFechamentoAsync("IDX", "F", Headers, _token);

        await _ecs.Received(1).CancelarPosicaoAsync(new EcsCancelarPosicaoRequest("IDX", "F"), Headers, _token);
    }

    // Configurado só para o token do teste: se o serviço não repassar o cancelamento, o ECS falso não responde.
    private void Detalhe(string? justificativaFechamento = null, string? justificativaBloco = null, string? justificativaPadrao = null) =>
        _ecs.GetHistoricoDetalheAsync("IDX", Headers, _token)
            .Returns(new EcsHistoricoDetalhe("P1", justificativaFechamento, 5m, justificativaBloco, 140m, 2m, justificativaPadrao, null, null, null));
}
