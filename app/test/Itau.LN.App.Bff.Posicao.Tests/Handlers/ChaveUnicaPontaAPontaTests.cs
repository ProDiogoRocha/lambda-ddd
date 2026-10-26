using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Application.Options;
using Itau.LN.App.Bff.Posicao.Application.Services;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Clients;
using Itau.LN.App.Bff.Posicao.Lambda.Handlers;
using Itau.LN.App.Bff.Posicao.Lambda.Options;
using Itau.LN.App.Bff.Posicao.Tests.Fakes;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Handlers;

// Handler, serviço e clients HTTP reais: só o banco, o token e o backend são falsos.
public class ChaveUnicaPontaAPontaTests
{
    private readonly ICredenciaisRepository _credenciais = Substitute.For<ICredenciaisRepository>();
    private readonly IAuthClient _auth = Substitute.For<IAuthClient>();

    public ChaveUnicaPontaAPontaTests()
    {
        _credenciais.ObterChaveUnicaAsync("TRANSP", "B1", Arg.Any<CancellationToken>()).Returns("chave-do-rds");
        _auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns("token");
    }

    [Fact]
    public async Task Posicao_BuscaABaseNoCentralizadorEEnviaAChaveDoRdsSoAoEcs()
    {
        var backend = new FakeHttpMessageHandler(request => FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            request.RequestUri!.AbsolutePath.StartsWith("/centralizadores/")
                ? """{"data":{"totalRegistros":"1","registros":[{"codigoBaseFornecedor":"B1","codigoPoloCentralizador":"40"}]}}"""
                : """{"data":{"numeroPosicao":"1","justificativaFechamento":"termo"}}"""));

        var response = await HandleAsync("/v1/posicao-transportadores/IDX/justificativa-termo-juridico", backend);

        Assert.Equal(200, response.StatusCode);
        Assert.Collection(backend.Requests,
            centralizador =>
            {
                Assert.Equal("/centralizadores/10/fornecedores/TRANSP", centralizador.RequestUri!.AbsolutePath);
                Assert.False(centralizador.Headers.Contains("chave-unica"));
            },
            ecs => Assert.Equal("chave-do-rds", ecs.Headers.GetValues("chave-unica").Single()));
    }

    [Fact]
    public async Task Centralizador_NaoEnviaChaveUnicaAoBackend()
    {
        var backend = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, """{"data":{"totalRegistros":"0"}}""");

        var response = await HandleAsync("/v1/centralizadores/10", backend);

        Assert.Equal(200, response.StatusCode);
        Assert.False(Assert.Single(backend.Requests).Headers.Contains("chave-unica"));
        Assert.Empty(_credenciais.ReceivedCalls());
    }

    // O front manda o contexto da posição e um header chave-unica próprio: o header nunca deve chegar ao backend.
    private Task<APIGatewayProxyResponse> HandleAsync(string path, FakeHttpMessageHandler backend)
    {
        var http = new HttpClient(backend) { BaseAddress = new Uri("https://backend.local/") };
        var centralizadores = new CentralizadoresClient(http, _auth);
        var handler = new BffRequestHandler(
            new PosicaoTransportadorService(new EcsPosicaoClient(http, _auth), Options.Create(new PosicaoOptions())),
            centralizadores,
            new ChaveUnicaService(centralizadores, _credenciais),
            Options.Create(new BffOptions()));

        return handler.HandleAsync(
            new APIGatewayProxyRequest
            {
                HttpMethod = "GET",
                Path = path,
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["centralizador"] = "10",
                    ["polo"] = "40",
                    ["transportadora"] = "TRANSP"
                },
                Headers = new Dictionary<string, string>
                {
                    ["x-itau-apikey"] = "api-key",
                    ["x-itau-correlationID"] = "corr-id",
                    ["chave-unica"] = "chave-do-header"
                }
            },
            Substitute.For<ILambdaContext>(),
            CancellationToken.None);
    }
}
