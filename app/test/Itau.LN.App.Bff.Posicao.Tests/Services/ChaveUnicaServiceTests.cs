using System.Net;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Centralizadores;
using Itau.LN.App.Bff.Posicao.Application.Exceptions;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Application.Services;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Services;

public class ChaveUnicaServiceTests
{
    private static readonly ApiRequestHeaders Headers = new("api-key", "corr-id", string.Empty);

    private readonly ICentralizadoresClient _centralizadores = Substitute.For<ICentralizadoresClient>();
    private readonly IChaveUnicaTokenGenerator _tokens = Substitute.For<IChaveUnicaTokenGenerator>();
    private readonly CancellationToken _token = new CancellationTokenSource().Token;
    private readonly ChaveUnicaService _service;

    public ChaveUnicaServiceTests() => _service = new ChaveUnicaService(_centralizadores, _tokens);

    [Fact]
    public async Task Obter_GeraJwtComATransportadoraEABaseDoPolo()
    {
        // A mesma base pode vir em mais de um registro (ex.: subpolos); outro polo, sem base ou sem fornecedor ficam de fora.
        Fornecedor(Registro("40", "B1"), Registro("40", "B1"), Registro("99", "B2"), Registro("40", null), Registro("40", "B3", fornecedor: null));
        _tokens.GerarAsync("123", "B1", _token).Returns("jwt");

        var chave = await _service.ObterAsync("10", "40", "123", Headers, _token);

        Assert.Equal("jwt", chave);
    }

    // O JWT leva o código como o centralizador devolve, não como o front mandou na query.
    [Fact]
    public async Task Obter_UsaOCodigoDoFornecedorDevolvidoPeloCentralizador()
    {
        Fornecedor(Registro("40", "B1", fornecedor: "000123"));
        _tokens.GerarAsync("000123", "B1", _token).Returns("jwt");

        Assert.Equal("jwt", await _service.ObterAsync("10", "40", "123", Headers, _token));
    }

    [Theory]
    [InlineData(false, "Base da transportadora nao encontrada no centralizador e polo informados.")]
    [InlineData(true, "Mais de uma base da transportadora no centralizador e polo informados.")]
    public async Task Obter_SemBaseUnicaNoPolo_Retorna400SemGerarJwt(bool duasBases, string mensagem)
    {
        Fornecedor(duasBases ? [Registro("40", "B1"), Registro("40", "B2")] : [Registro("99", "B1")]);

        var ex = await Assert.ThrowsAsync<ApiHttpException>(() => _service.ObterAsync("10", "40", "123", Headers, _token));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(mensagem, ex.Message);
        Assert.Empty(_tokens.ReceivedCalls());
    }

    [Fact]
    public async Task Obter_FornecedorSemRegistros_Retorna400()
    {
        _centralizadores.GetFornecedorAsync("10", "123", Headers, _token).Returns(new CentralizadorResponse("0", null));

        var ex = await Assert.ThrowsAsync<ApiHttpException>(() => _service.ObterAsync("10", "40", "123", Headers, _token));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    private void Fornecedor(params CentralizadorRegistro[] registros) =>
        _centralizadores.GetFornecedorAsync("10", "123", Headers, _token)
            .Returns(new CentralizadorResponse(registros.Length.ToString(), registros));

    private static CentralizadorRegistro Registro(string polo, string? codigoBase, string? fornecedor = "123") =>
        new("10", fornecedor, codigoBase, null, polo, null, null, null, null, null, null, null, null, null, null, null);
}
