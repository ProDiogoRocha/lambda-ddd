using System.Net;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Exceptions;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Clients;
using Itau.LN.App.Bff.Posicao.Tests.Fakes;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Clients;

public class EcsPosicaoClientTests
{
    private static readonly ApiRequestHeaders Headers = new("api-key", "corr-id", "chave");

    private readonly IAuthClient _auth = Substitute.For<IAuthClient>();

    public EcsPosicaoClientTests() => _auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns("token-123");

    [Fact]
    public async Task Get_EnviaTokenEHeadersEDesembrulhaData()
    {
        var http = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, """{"data":{"numeroPosicao":"7","valorDivergenciaBloco":10.5}}""");
        var client = new EcsPosicaoClient(CriarHttpClient(http), _auth);

        var detalhe = await client.GetHistoricoDetalheAsync("A B", Headers, CancellationToken.None);

        Assert.Equal("7", detalhe.NumeroPosicao);
        Assert.Equal(10.5m, detalhe.ValorDivergenciaBloco);
        var request = Assert.Single(http.Requests);
        Assert.Equal(
            "https://api.local/base/custodias-posicoes-historicos-detalhes-consultar?indiceControlePosicao=A%20B",
            request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer token-123", request.Headers.Authorization!.ToString());
        Assert.Equal("api-key", request.Headers.GetValues("x-itau-apikey").Single());
        Assert.Equal("corr-id", request.Headers.GetValues("x-itau-correlationID").Single());
        Assert.Equal("chave", request.Headers.GetValues("chave-unica").Single());
    }

    [Fact]
    public async Task GetLista_SemChaveUnica_ConsultaPelaDataSemEnviarHeader()
    {
        var http = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, """{"data":{"ocorrencias":[]}}""");
        var client = new EcsPosicaoClient(CriarHttpClient(http), _auth);

        await client.GetHistoricosListaAsync("2026-10-25", Headers with { ChaveUnica = "" }, CancellationToken.None);

        var request = Assert.Single(http.Requests);
        Assert.False(request.Headers.Contains("chave-unica"));
        Assert.Equal(
            "https://api.local/base/custodias-posicoes-historicos-listas-consultar?dataMovimento=2026-10-25",
            request.RequestUri!.AbsoluteUri);
    }

    // Os testes de handler e mapper montam os records por posição; aqui o nome de cada campo do ECS fica fixado.
    [Fact]
    public async Task GetLista_DesserializaTodosOsCamposDaOcorrencia()
    {
        var http = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, """{"data":{"ocorrencias":[{"indiceControlePosicao":"IDX","descricaoPosicao":"Tipo","descricaoSituacaoPosicao":"Fechada","valorSaldoAnteriorComposicao":1,"valorSaldoAtualComposicao":2,"dataCadastro":"2026-10-25","horaCadastro":"10:00"}]}}""");

        var lista = await new EcsPosicaoClient(CriarHttpClient(http), _auth).GetHistoricosListaAsync("2026-10-25", Headers, CancellationToken.None);

        Assert.Equal(new EcsHistoricoListaOcorrencia("IDX", "Tipo", "Fechada", 1m, 2m, "2026-10-25", "10:00"), Assert.Single(lista.Ocorrencias!));
    }

    [Fact]
    public async Task GetDetalhe_DesserializaBlocosCedulasEMoedas()
    {
        var http = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, """{"data":{"numeroPosicao":"P1","blocoOcorrenciaLista":[{"descricaoItemBloco":"B","valorEntradaItemBloco":1,"valorDivergenciaEntradaBloco":2,"valorSaidaItemBloco":3,"valorDivergenciaSaidaBloco":4}],"cedulasOcorrenciaLista":[{"codigoClassificacaoCedula":"C","valorFaceCedula":100,"valorInformadoCedula":5,"valorDivergenciaItemCedula":6}],"moedasOcorrenciaLista":[{"codigoClassificacaoMoeda":"M","valorFaceMoeda":1,"valorInformadoMoeda":7,"valorDivergenciaItemMoeda":8}]}}""");

        var detalhe = await new EcsPosicaoClient(CriarHttpClient(http), _auth).GetHistoricoDetalheAsync("IDX", Headers, CancellationToken.None);

        Assert.Equal(new EcsBlocoOcorrencia("B", 1m, 2m, 3m, 4m), Assert.Single(detalhe.BlocoOcorrenciaLista!));
        Assert.Equal(new EcsCedulaOcorrencia("C", 100m, 5m, 6m), Assert.Single(detalhe.CedulasOcorrenciaLista!));
        Assert.Equal(new EcsMoedaOcorrencia("M", 1m, 7m, 8m), Assert.Single(detalhe.MoedasOcorrenciaLista!));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("<html>OK</html>")]
    public async Task Get_SemCampoData_LancaErro500(string body)
    {
        var client = new EcsPosicaoClient(CriarHttpClient(FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, body)), _auth);

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
        var client = new EcsPosicaoClient(CriarHttpClient(FakeHttpMessageHandler.Respondendo(HttpStatusCode.BadGateway, body)), _auth);

        var ex = await Assert.ThrowsAsync<ApiHttpException>(
            () => client.GetHistoricoDetalheAsync("IDX", Headers, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Equal(mensagem, ex.Message);
        // Para o log: método, host e caminho da chamada, sem a query string.
        Assert.Equal("GET api.local/base/custodias-posicoes-historicos-detalhes-consultar", ex.Destino);
    }

    [Fact]
    public async Task Cancelar_AceitaRespostaSemCorpoEEnviaJsonCamelCase()
    {
        var http = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new EcsPosicaoClient(CriarHttpClient(http), _auth);

        await client.CancelarPosicaoAsync(new EcsCancelarPosicaoRequest("IDX", "F"), Headers, CancellationToken.None);

        var request = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.local/base/custodia-posicoes-cancelamento", request.RequestUri!.AbsoluteUri);
        Assert.Equal("""{"indiceControlePosicao":"IDX","codigoTipoPosicao":"F"}""", Assert.Single(http.Bodies));
    }

    private static HttpClient CriarHttpClient(FakeHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.local/base/") };
}
