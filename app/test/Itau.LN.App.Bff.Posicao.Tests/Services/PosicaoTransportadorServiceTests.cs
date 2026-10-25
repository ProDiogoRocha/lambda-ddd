using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Application.Options;
using Itau.LN.App.Bff.Posicao.Application.Services;
using Itau.LN.App.Bff.Posicao.Domain.Models;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Services;

// Cards (paralelismo e ordem) são cobertos pelos testes do BffRequestHandler, que usa este serviço real.
public class PosicaoTransportadorServiceTests
{
    private static readonly ApiRequestHeaders Headers = new("api-key", "corr-id", "chave");

    private readonly IEcsPosicaoClient _ecs = Substitute.For<IEcsPosicaoClient>();
    private readonly PosicaoTransportadorService _service;

    public PosicaoTransportadorServiceTests() =>
        _service = new PosicaoTransportadorService(_ecs, Options.Create(new PosicaoOptions()));

    [Fact]
    public async Task ConsultarDivergencias_MapeiaTotaisEJustificativas()
    {
        Detalhe(justificativaBloco: "bloco", justificativaPadrao: "padrao");

        var divergencias = await _service.ConsultarDivergenciasAsync("IDX", Headers, CancellationToken.None);

        Assert.Equal(new JustificativaDivergencias("IDX", 5m, "bloco", 2m, "padrao"), divergencias);
    }

    [Fact]
    public async Task ConsultarDivergencias_SemJustificativas_UsaTextoVazio()
    {
        Detalhe();

        var divergencias = await _service.ConsultarDivergenciasAsync("IDX", Headers, CancellationToken.None);

        Assert.Equal(new JustificativaDivergencias("IDX", 5m, "", 2m, ""), divergencias);
    }

    [Theory]
    [InlineData("termo", "termo")]
    [InlineData(null, "")]
    public async Task ConsultarTermoJuridico_UsaJustificativaDeFechamento(string? justificativa, string esperado)
    {
        Detalhe(justificativaFechamento: justificativa);

        var termo = await _service.ConsultarTermoJuridicoAsync("IDX", Headers, CancellationToken.None);

        Assert.Equal(new TermoJuridico("IDX", esperado), termo);
    }

    [Fact]
    public async Task ReverterFechamento_CancelaAPosicaoNoEcs()
    {
        await _service.ReverterFechamentoAsync("IDX", "F", Headers, CancellationToken.None);

        await _ecs.Received(1).CancelarPosicaoAsync(new EcsCancelarPosicaoRequest("IDX", "F"), Headers, Arg.Any<CancellationToken>());
    }

    private void Detalhe(string? justificativaFechamento = null, string? justificativaBloco = null, string? justificativaPadrao = null) =>
        _ecs.GetHistoricoDetalheAsync("IDX", Headers, Arg.Any<CancellationToken>())
            .Returns(new EcsHistoricoDetalhe("P1", justificativaFechamento, 5m, justificativaBloco, 140m, 2m, justificativaPadrao, null, null, null));
}
