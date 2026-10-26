using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Centralizadores;
using Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;
using Itau.LN.App.Bff.Posicao.Application.Exceptions;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Application.Options;
using Itau.LN.App.Bff.Posicao.Application.Services;
using Itau.LN.App.Bff.Posicao.Lambda.Handlers;
using Itau.LN.App.Bff.Posicao.Lambda.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Itau.LN.App.Bff.Posicao.Tests.Handlers;

public class BffRequestHandlerTests
{
    private static readonly ApiRequestHeaders HeadersSemChave = new("api-key", "corr-id", string.Empty);
    private static readonly ApiRequestHeaders HeadersPosicao = HeadersSemChave with { ChaveUnica = "chave-do-rds" };

    private readonly IEcsPosicaoClient _ecs = Substitute.For<IEcsPosicaoClient>();
    private readonly ICentralizadoresClient _centralizadores = Substitute.For<ICentralizadoresClient>();
    private readonly ICredenciaisRepository _credenciais = Substitute.For<ICredenciaisRepository>();
    private readonly ILambdaContext _context = Substitute.For<ILambdaContext>();
    private readonly CancellationToken _token = new CancellationTokenSource().Token;

    private const string RotaTermo = "/v1/posicao-transportadores/IDX/justificativa-termo-juridico";

    // Contexto padrão das rotas de posição (ver Contexto()): base B1 no polo 40, com chave no banco.
    public BffRequestHandlerTests()
    {
        _centralizadores.GetFornecedorAsync("10", "TRANSP", HeadersSemChave, Arg.Any<CancellationToken>())
            .Returns(new CentralizadorResponse("1", [
                new("10", "TRANSP", "B1", null, "40", null, null, null, null, null, null, null, null, null, null, null)]));
        _credenciais.ObterChaveUnicaAsync("TRANSP", "B1", Arg.Any<CancellationToken>()).Returns("chave-do-rds");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("api-key", null)]
    [InlineData(null, "corr-id")]
    [InlineData(" ", "corr-id")]
    public async Task SemHeadersObrigatorios_Retorna400AntesDoRoteamento(string? apiKey, string? correlationId)
    {
        var headers = new Dictionary<string, string>();
        if (apiKey is not null)
        {
            headers["x-itau-apikey"] = apiKey;
        }

        if (correlationId is not null)
        {
            headers["x-itau-correlationID"] = correlationId;
        }

        var response = await HandleAsync(Request("GET", "/v1/rota-inexistente", headers: headers));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            """{"messages":[{"codigo":"400","mensagem":"Headers obrigatorios ausentes: x-itau-apikey e x-itau-correlationID."}]}""",
            response.Body);
    }

    [Fact]
    public async Task ComHeadersNulos_Retorna400()
    {
        var request = Request("GET", "/v1/posicao-transportadores");
        request.Headers = null;

        var response = await HandleAsync(request);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Headers obrigatorios ausentes: x-itau-apikey e x-itau-correlationID.", Mensagem(response));
    }

    [Theory]
    [InlineData(null, "Parametro obrigatorio ausente: data.")]
    [InlineData(" ", "Parametro obrigatorio ausente: data.")]
    [InlineData("25/10/2026", "Parametro data invalido. Utilize o formato yyyy-MM-dd.")]
    public async Task ConsultarCards_ComDataAusenteOuInvalida_Retorna400SemBuscarAChave(string? data, string mensagem)
    {
        var query = Contexto();
        if (data is not null)
        {
            query["data"] = data;
        }

        var response = await HandleAsync(Request("GET", "/v1/posicao-transportadores", query));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(mensagem, Mensagem(response));
        Assert.Empty(_centralizadores.ReceivedCalls());
        Assert.Empty(_credenciais.ReceivedCalls());
    }

    [Fact]
    public async Task ConsultarCards_MontaUmCardPorOcorrenciaNaOrdemDaLista()
    {
        _ecs.GetHistoricosListaAsync("2026-10-25", HeadersPosicao, Arg.Any<CancellationToken>())
            .Returns(new EcsHistoricoLista([Ocorrencia("A"), Ocorrencia("B")]));
        // "A" responde depois de "B": a ordem dos cards tem que vir da lista, não de quem termina primeiro.
        _ecs.GetHistoricoDetalheAsync("A", HeadersPosicao, Arg.Any<CancellationToken>())
            .Returns(async _ => { await Task.Delay(50); return Detalhe("posicao-A"); });
        _ecs.GetHistoricoDetalheAsync("B", HeadersPosicao, Arg.Any<CancellationToken>()).Returns(Detalhe("posicao-B"));

        var response = await HandleAsync(Request("GET", "/v1/posicao-transportadores", Data("2026-10-25")));

        Assert.Equal(200, response.StatusCode);
        var cards = Json(response).GetProperty("cards");
        Assert.Equal(["A", "B"], cards.EnumerateArray().Select(card => card.GetProperty("indiceControlePosicao").GetString()));
        Assert.Equal("posicao-A", cards[0].GetProperty("posicao").GetString());
        Assert.Equal("NA", cards[0].GetProperty("operador").GetString());
        // Uma ida ao banco por request, não uma por detalhe.
        await _credenciais.Received(1).ObterChaveUnicaAsync("TRANSP", "B1", _token);
    }

    [Fact]
    public async Task ConsultarCards_ComListaSemOcorrencias_RetornaCardsVazio()
    {
        _ecs.GetHistoricosListaAsync(default!, default!, default).ReturnsForAnyArgs(new EcsHistoricoLista(null));

        var response = await HandleAsync(Request("GET", "/v1/posicao-transportadores", Data("2026-10-25")));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"cards":[]}""", response.Body);
    }

    [Fact]
    public async Task ConsultarCards_ComErroEmUmDetalhe_RepassaOErro()
    {
        _ecs.GetHistoricosListaAsync(default!, default!, default)
            .ReturnsForAnyArgs(new EcsHistoricoLista([Ocorrencia("A"), Ocorrencia("B")]));
        _ecs.GetHistoricoDetalheAsync("A", Arg.Any<ApiRequestHeaders>(), Arg.Any<CancellationToken>()).Returns(Detalhe("1"));
        _ecs.GetHistoricoDetalheAsync("B", Arg.Any<ApiRequestHeaders>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiHttpException(HttpStatusCode.NotFound, "Posicao B nao encontrada"));

        var response = await HandleAsync(Request("GET", "/v1/posicao-transportadores", Data("2026-10-25")));

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Posicao B nao encontrada", Mensagem(response));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nao-e-json")]
    [InlineData("{}")]
    [InlineData("""{"codigoTipoPosicao":" "}""")]
    public async Task Reverter_SemCodigoTipoPosicao_Retorna400(string? body)
    {
        var response = await HandleAsync(Request("POST", "/v1/posicao-transportadores/IDX/reverter", body: body));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Campo obrigatorio ausente no body: codigoTipoPosicao.", Mensagem(response));
        await _ecs.DidNotReceiveWithAnyArgs().CancelarPosicaoAsync(default!, default!, default);
    }

    [Fact]
    public async Task Reverter_CancelaPosicaoNoEcs()
    {
        var response = await HandleAsync(Request(
            "POST", "/v1/posicao-transportadores/IDX/reverter", body: """{"codigoTipoPosicao":"F"}"""));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"sucesso":true,"mensagem":"Reversao realizada com sucesso","indiceControlePosicao":"IDX"}""", response.Body);
        await _ecs.Received(1).CancelarPosicaoAsync(
            new EcsCancelarPosicaoRequest("IDX", "F"), HeadersPosicao, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JustificativaDivergencias_MapeiaDetalheDoEcs()
    {
        _ecs.GetHistoricoDetalheAsync("IDX", HeadersPosicao, Arg.Any<CancellationToken>()).Returns(Detalhe("1"));

        var response = await HandleAsync(Request("GET", "/v1/posicao-transportadores/IDX/justificativa-divergencias"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(
            """{"indiceControlePosicao":"IDX","totalDivergenciasMovimentacoes":5,"justificativaDivergenciasMovimentacoes":"bloco","totalDivergenciaPadrao":2,"justificativaDivergenciaPadrao":""}""",
            response.Body);
    }

    [Fact]
    public async Task JustificativaTermoJuridico_SemBasePath_UsaJustificativaDeFechamento()
    {
        _ecs.GetHistoricoDetalheAsync("IDX", HeadersPosicao, Arg.Any<CancellationToken>()).Returns(Detalhe("1"));

        var response = await HandleAsync(Request("GET", "/posicao-transportadores/IDX/justificativa-termo-juridico"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"indiceControlePosicao":"IDX","justificativaTermoJuridico":"fechamento"}""", response.Body);
    }

    [Theory]
    [InlineData("/v1", "/V1/posicao-transportadores/IDX/justificativa-termo-juridico")]
    [InlineData("/api/v2", "/api/v2/posicao-transportadores/IDX/justificativa-termo-juridico")]
    public async Task BasePath_VemDaConfiguracaoESaiSemDiferenciarCaixa(string apiBasePath, string path)
    {
        _ecs.GetHistoricoDetalheAsync("IDX", HeadersPosicao, Arg.Any<CancellationToken>()).Returns(Detalhe("1"));

        var response = await HandleAsync(Request("GET", path), apiBasePath: apiBasePath);

        Assert.Equal(200, response.StatusCode);
    }

    [Theory]
    [InlineData("/v1/centralizadores/10", "centralizador")]
    [InlineData("/v1/centralizadores/10/fornecedores/20", "fornecedor")]
    [InlineData("/v1/centralizadores/10/fornecedores/20/bases/30", "base")]
    [InlineData("/v1/centralizadores/10/polos/40", "polo")]
    public async Task Centralizadores_EncaminhaParaOMetodoDaRotaSemChaveUnica(string path, string totalEsperado)
    {
        // Mesmo com o contexto da posição (padrão do Request) e header chave-unica, centralizador não leva chave nem vai ao banco.
        _centralizadores.GetCentralizadorAsync("10", HeadersSemChave, Arg.Any<CancellationToken>())
            .Returns(new CentralizadorResponse("centralizador", []));
        _centralizadores.GetFornecedorAsync("10", "20", HeadersSemChave, Arg.Any<CancellationToken>())
            .Returns(new CentralizadorResponse("fornecedor", []));
        _centralizadores.GetFornecedorInBaseAsync("10", "20", "30", HeadersSemChave, Arg.Any<CancellationToken>())
            .Returns(new CentralizadorResponse("base", []));
        _centralizadores.GetPoloAsync("10", "40", HeadersSemChave, Arg.Any<CancellationToken>())
            .Returns(new CentralizadorResponse("polo", []));
        var request = Request("GET", path);
        request.Headers["chave-unica"] = "chave-do-header";

        var response = await HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(totalEsperado, Json(response).GetProperty("totalRegistros").GetString());
        Assert.Single(_centralizadores.ReceivedCalls());
        Assert.Empty(_credenciais.ReceivedCalls());
    }

    [Theory]
    [InlineData("GET", "/v1/rota-inexistente")]
    [InlineData("DELETE", "/v1/posicao-transportadores")]
    [InlineData("POST", "/v1/posicao-transportadores//reverter")]
    [InlineData("GET", "/v1/POSICAO-TRANSPORTADORES")]
    [InlineData("GET", "/v1/centralizadores/10/polos/..")]
    [InlineData(null, "/v1/posicao-transportadores")]
    [InlineData("GET", null)]
    public async Task RotaInexistente_Retorna404SemConsultarAsApis(string? method, string? path)
    {
        var response = await HandleAsync(Request(method, path));

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("""{"messages":[{"codigo":"404","mensagem":"Recurso inexistente."}]}""", response.Body);
        Assert.Empty(_credenciais.ReceivedCalls());
        Assert.Empty(_centralizadores.ReceivedCalls());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 500)]
    public async Task ErroDaApiDeDestino_RepassaMensagemComStatusMapeado(HttpStatusCode statusDaApi, int statusEsperado)
    {
        _ecs.GetHistoricoDetalheAsync(default!, default!, default)
            .ThrowsAsyncForAnyArgs(new ApiHttpException(statusDaApi, "mensagem do ECS"));

        var response = await HandleAsync(Request("GET", "/v1/posicao-transportadores/IDX/justificativa-divergencias"));

        Assert.Equal(statusEsperado, response.StatusCode);
        Assert.Equal("mensagem do ECS", Mensagem(response));
    }

    [Fact]
    public async Task ErroInesperado_Retorna500Generico()
    {
        _ecs.GetHistoricoDetalheAsync(default!, default!, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("detalhe interno"));

        var response = await HandleAsync(Request("GET", "/v1/posicao-transportadores/IDX/justificativa-divergencias"));

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("Erro interno.", Mensagem(response));
    }

    [Fact]
    public async Task ChaveUnica_SemChaveNoBanco_Retorna400SemChamarOEcs()
    {
        _credenciais.ObterChaveUnicaAsync(default!, default!, default).ReturnsForAnyArgs((string?)null);

        var response = await HandleAsync(Request("GET", RotaTermo));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Chave de acesso nao cadastrada para a transportadora informada.", Mensagem(response));
        Assert.Empty(_ecs.ReceivedCalls());
    }

    [Fact]
    public async Task ChaveUnica_ComFalhaNoBanco_Retorna500Generico()
    {
        _credenciais.ObterChaveUnicaAsync(default!, default!, default).ThrowsAsyncForAnyArgs(new InvalidOperationException("RDS fora"));

        var response = await HandleAsync(Request("GET", RotaTermo));

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("Erro interno.", Mensagem(response));
    }

    [Theory]
    [InlineData("centralizador", null)]
    [InlineData("polo", null)]
    [InlineData("transportadora", null)]
    [InlineData("transportadora", " ")]
    public async Task ChaveUnica_PosicaoSemContextoCompleto_Retorna400IgnorandoOHeader(string parametro, string? valor)
    {
        var query = Contexto();
        query.Remove(parametro);
        if (valor is not null)
        {
            query[parametro] = valor;
        }

        var request = Request("GET", RotaTermo, query);
        request.Headers["chave-unica"] = "chave-do-header";

        var response = await HandleAsync(request);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Parametros obrigatorios ausentes: centralizador, polo e transportadora.", Mensagem(response));
        Assert.Empty(_ecs.ReceivedCalls());
        Assert.Empty(_centralizadores.ReceivedCalls());
        Assert.Empty(_credenciais.ReceivedCalls());
    }

    [Fact]
    public async Task ComCorsConfigurado_SucessoEErroLevamAOrigemPermitida()
    {
        const string origem = "https://front.exemplo.com";
        _ecs.GetHistoricoDetalheAsync(default!, default!, default).ReturnsForAnyArgs(Detalhe("posicao"));

        var sucesso = await HandleAsync(Request("GET", "/v1/posicao-transportadores/IDX/justificativa-divergencias"), corsAllowedOrigin: origem);
        var erro = await HandleAsync(Request("GET", "/v1/rota-inexistente"), corsAllowedOrigin: origem);

        Assert.Equal(200, sucesso.StatusCode);
        Assert.Equal(origem, sucesso.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal(404, erro.StatusCode);
        Assert.Equal(origem, erro.Headers["Access-Control-Allow-Origin"]);
    }

    [Fact]
    public async Task SemCorsConfigurado_NaoEnviaHeaderCors()
    {
        var response = await HandleAsync(Request("GET", "/v1/rota-inexistente"));

        Assert.False(response.Headers.ContainsKey("Access-Control-Allow-Origin"));
    }

    private Task<APIGatewayProxyResponse> HandleAsync(
        APIGatewayProxyRequest request, string? corsAllowedOrigin = null, string apiBasePath = "/v1")
    {
        var handler = new BffRequestHandler(
            new PosicaoTransportadorService(_ecs, Options.Create(new PosicaoOptions())),
            _centralizadores,
            new ChaveUnicaService(_centralizadores, _credenciais),
            Options.Create(new BffOptions
            {
                ApiBasePath = apiBasePath,
                CorsAllowedOrigin = corsAllowedOrigin
            }));

        return handler.HandleAsync(request, _context, _token);
    }

    private static APIGatewayProxyRequest Request(
        string? method,
        string? path,
        Dictionary<string, string>? query = null,
        string? body = null,
        Dictionary<string, string>? headers = null) => new()
        {
            HttpMethod = method,
            Path = path,
            QueryStringParameters = query ?? Contexto(),
            Body = body,
            // Caixa diferente de propósito: a busca de header não diferencia maiúsculas.
            Headers = headers ?? new() { ["X-Itau-ApiKey"] = "api-key", ["x-itau-correlationid"] = "corr-id" }
        };

    // Query que as rotas de posição recebem para achar a chave-unica (ver construtor).
    private static Dictionary<string, string> Contexto() =>
        new() { ["centralizador"] = "10", ["polo"] = "40", ["transportadora"] = "TRANSP" };

    private static Dictionary<string, string> Data(string data) => new(Contexto()) { ["data"] = data };

    private static JsonElement Json(APIGatewayProxyResponse response) => JsonDocument.Parse(response.Body).RootElement;

    private static string? Mensagem(APIGatewayProxyResponse response) =>
        Json(response).GetProperty("messages")[0].GetProperty("mensagem").GetString();

    private static EcsHistoricoListaOcorrencia Ocorrencia(string indice) =>
        new(indice, "Tipo", "Fechada", 100m, 150m, "2026-10-25", "10:00");

    private static EcsHistoricoDetalhe Detalhe(string numeroPosicao) =>
        new(numeroPosicao, "fechamento", 5m, "bloco", 140m, 2m, null, null, null, null);
}
