using System.Net;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Exceptions;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Clients;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests;

public class ApiClientsTests
{
    private static readonly ApiRequestHeaders Headers = new("api-key", "corr-id", "chave");

    private readonly IAuthClient _auth = Substitute.For<IAuthClient>();

    public ApiClientsTests() => _auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns("token-123");

    [Fact]
    public async Task Get_EnviaTokenEHeadersEDesembrulhaData()
    {
        var http = Responder(HttpStatusCode.OK, """{"data":{"numeroPosicao":"7","valorDivergenciaBloco":10.5}}""");
        var client = new EcsPosicaoClient(CriarHttpClient(http), _auth);

        var detalhe = await client.GetHistoricoDetalheAsync("A B", Headers, CancellationToken.None);

        Assert.Equal("7", detalhe.NumeroPosicao);
        Assert.Equal(10.5m, detalhe.ValorDivergenciaBloco);
        var request = Assert.Single(http.Requests);
        Assert.Equal(
            "https://api.local/base/posicaonumerarionacional/v2/custodias-posicoes-historicos-detalhes-consultar?indiceControlePosicao=A%20B",
            request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer token-123", request.Headers.Authorization!.ToString());
        Assert.Equal("api-key", request.Headers.GetValues("x-itau-apikey").Single());
        Assert.Equal("corr-id", request.Headers.GetValues("x-itau-correlationID").Single());
        Assert.Equal("chave", request.Headers.GetValues("chave-unica").Single());
    }

    [Fact]
    public async Task Get_SemChaveUnica_NaoEnviaHeader()
    {
        var http = Responder(HttpStatusCode.OK, """{"data":{"ocorrencias":[]}}""");
        var client = new EcsPosicaoClient(CriarHttpClient(http), _auth);

        await client.GetHistoricosListaAsync("2026-10-25", Headers with { ChaveUnica = "" }, CancellationToken.None);

        Assert.False(Assert.Single(http.Requests).Headers.Contains("chave-unica"));
    }

    [Fact]
    public async Task Get_SemCampoData_LancaErro500()
    {
        var client = new EcsPosicaoClient(CriarHttpClient(Responder(HttpStatusCode.OK, "{}")), _auth);

        var ex = await Assert.ThrowsAsync<ApiHttpException>(
            () => client.GetHistoricoDetalheAsync("IDX", Headers, CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal("Resposta inválida da API de destino: payload sem campo data.", ex.Message);
    }

    [Theory]
    [InlineData("""{"mensagem":"Mensagem","detalhe":"Detalhe"}""", "Detalhe")]
    [InlineData("""{"mensagem":"Mensagem"}""", "Mensagem")]
    [InlineData("<html>Bad Gateway</html>", "Erro HTTP 502")]
    [InlineData("", "Erro HTTP 502")]
    public async Task ErroHttp_LancaApiHttpExceptionComStatusEMensagem(string body, string mensagem)
    {
        var client = new EcsPosicaoClient(CriarHttpClient(Responder(HttpStatusCode.BadGateway, body)), _auth);

        var ex = await Assert.ThrowsAsync<ApiHttpException>(
            () => client.GetHistoricoDetalheAsync("IDX", Headers, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Equal(mensagem, ex.Message);
    }

    [Fact]
    public async Task Cancelar_AceitaRespostaSemCorpoEEnviaJsonCamelCase()
    {
        var http = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new EcsPosicaoClient(CriarHttpClient(http), _auth);

        await client.CancelarPosicaoAsync(new EcsCancelarPosicaoRequest("IDX", "F"), Headers, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, Assert.Single(http.Requests).Method);
        Assert.Equal("""{"indiceControlePosicao":"IDX","codigoTipoPosicao":"F"}""", Assert.Single(http.Bodies));
    }

    [Fact]
    public async Task Centralizadores_EscapaSegmentosEDesserializaRegistros()
    {
        var http = Responder(
            HttpStatusCode.OK,
            """{"data":{"totalRegistros":"1","registros":[{"codigoCentralizador":"10","nomeBaseFornecedor":"Base"}]}}""");
        var client = new CentralizadoresClient(CriarHttpClient(http), _auth);

        var response = await client.GetFornecedorInBaseAsync("10", "F 1", "B?2", Headers, CancellationToken.None);

        Assert.Equal(
            "https://api.local/base/centralizadores/10/fornecedores/F%201/bases/B%3F2",
            Assert.Single(http.Requests).RequestUri!.AbsoluteUri);
        Assert.Equal("1", response.TotalRegistros);
        Assert.Equal("Base", Assert.Single(response.Registros!).NomeBaseFornecedor);
    }

    private static FakeHttpMessageHandler Responder(HttpStatusCode statusCode, string body) =>
        new(_ => FakeHttpMessageHandler.Json(statusCode, body));

    private static HttpClient CriarHttpClient(FakeHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.local/base/") };
}
