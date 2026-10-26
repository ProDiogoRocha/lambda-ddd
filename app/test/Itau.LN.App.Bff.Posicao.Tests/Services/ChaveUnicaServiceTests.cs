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
    private readonly ICredenciaisRepository _credenciais = Substitute.For<ICredenciaisRepository>();
    private readonly CancellationToken _token = new CancellationTokenSource().Token;
    private readonly ChaveUnicaService _service;

    public ChaveUnicaServiceTests() => _service = new ChaveUnicaService(_centralizadores, _credenciais);

    [Fact]
    public async Task Obter_BuscaNoBancoPelaBaseDaTransportadoraNoPolo()
    {
        // A mesma base pode vir em mais de um registro (ex.: subpolos); bases de outro polo ficam de fora.
        Fornecedor(Registro("40", "B1"), Registro("40", "B1"), Registro("99", "B2"), Registro("40", null));
        _credenciais.ObterChaveUnicaAsync("TRANSP", "B1", _token).Returns("chave-do-rds");

        var chave = await _service.ObterAsync("10", "40", "TRANSP", Headers, _token);

        Assert.Equal("chave-do-rds", chave);
    }

    [Theory]
    [InlineData(false, "Base da transportadora nao encontrada no centralizador e polo informados.")]
    [InlineData(true, "Mais de uma base da transportadora no centralizador e polo informados.")]
    public async Task Obter_SemBaseUnicaNoPolo_Retorna400SemConsultarOBanco(bool duasBases, string mensagem)
    {
        Fornecedor(duasBases ? [Registro("40", "B1"), Registro("40", "B2")] : [Registro("99", "B1")]);

        var ex = await Assert.ThrowsAsync<ApiHttpException>(() => _service.ObterAsync("10", "40", "TRANSP", Headers, _token));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(mensagem, ex.Message);
        Assert.Empty(_credenciais.ReceivedCalls());
    }

    [Fact]
    public async Task Obter_FornecedorSemRegistros_Retorna400()
    {
        _centralizadores.GetFornecedorAsync("10", "TRANSP", Headers, _token).Returns(new CentralizadorResponse("0", null));

        var ex = await Assert.ThrowsAsync<ApiHttpException>(() => _service.ObterAsync("10", "40", "TRANSP", Headers, _token));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task Obter_SemChaveNoBanco_Retorna400(string? chaveDoBanco)
    {
        Fornecedor(Registro("40", "B1"));
        _credenciais.ObterChaveUnicaAsync("TRANSP", "B1", _token).Returns(chaveDoBanco);

        var ex = await Assert.ThrowsAsync<ApiHttpException>(() => _service.ObterAsync("10", "40", "TRANSP", Headers, _token));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Chave de acesso nao cadastrada para a transportadora informada.", ex.Message);
    }

    private void Fornecedor(params CentralizadorRegistro[] registros) =>
        _centralizadores.GetFornecedorAsync("10", "TRANSP", Headers, _token)
            .Returns(new CentralizadorResponse(registros.Length.ToString(), registros));

    private static CentralizadorRegistro Registro(string polo, string? codigoBase) =>
        new("10", "TRANSP", codigoBase, null, polo, null, null, null, null, null, null, null, null, null, null, null);
}
