using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Itau.LN.App.Bff.Posicao.Application.Options;
using Itau.LN.App.Bff.Posicao.Application.Services;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Clients;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Lambda.Handlers;
using Itau.LN.App.Bff.Posicao.Lambda.Options;
using Itau.LN.App.Bff.Posicao.Tests.Fakes;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Handlers;

// Handler, serviço, gerador do JWT e clients HTTP reais: só o Secrets Manager, o token OAuth e o backend são falsos.
public class ChaveUnicaPontaAPontaTests
{
    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();
    private readonly IAuthClient _auth = Substitute.For<IAuthClient>();

    public ChaveUnicaPontaAPontaTests()
    {
        _secretsManager.GetSecretValueAsync(default!, default)
            .ReturnsForAnyArgs(new GetSecretValueResponse { SecretString = "segredo-de-teste" });
        _auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns("token");
    }

    [Fact]
    public async Task Posicao_BuscaATransportadoraEABaseNoCentralizadorEEnviaOJwtSoAoEcs()
    {
        var backend = new FakeHttpMessageHandler(request => FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            request.RequestUri!.AbsolutePath.StartsWith("/centralizadores/")
                ? """{"data":{"totalRegistros":"1","registros":[{"codigoFornecedor":"000123","codigoBaseFornecedor":"B1","codigoPoloCentralizador":"40"}]}}"""
                : """{"data":{"numeroPosicao":"1","justificativaFechamento":"termo"}}"""));

        var response = await HandleAsync("/v1/posicao-transportadores/IDX/justificativa-termo-juridico", backend);

        Assert.Equal(200, response.StatusCode);
        Assert.Collection(backend.Requests,
            centralizador =>
            {
                Assert.Equal("/centralizadores/10/fornecedores/123", centralizador.RequestUri!.AbsolutePath);
                Assert.False(centralizador.Headers.Contains("chave-unica"));
            },
            ecs =>
            {
                var payload = ecs.Headers.GetValues("chave-unica").Single().Split('.')[1];
                using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(payload));
                Assert.Equal("000123", claims.RootElement.GetProperty("codigo_transportadora").GetString());
                Assert.Equal("B1", claims.RootElement.GetProperty("codigo_filial").GetString());
            });
    }

    [Fact]
    public async Task Centralizador_NaoEnviaChaveUnicaAoBackend()
    {
        var backend = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, """{"data":{"totalRegistros":"0"}}""");

        var response = await HandleAsync("/v1/centralizadores/10", backend);

        Assert.Equal(200, response.StatusCode);
        Assert.False(Assert.Single(backend.Requests).Headers.Contains("chave-unica"));
        Assert.Empty(_secretsManager.ReceivedCalls());
    }

    // O front manda o contexto da posição e um header chave-unica próprio: o header nunca deve chegar ao backend.
    private Task<APIGatewayProxyResponse> HandleAsync(string path, FakeHttpMessageHandler backend)
    {
        var http = new HttpClient(backend) { BaseAddress = new Uri("https://backend.local/") };
        var centralizadores = new CentralizadoresClient(http, _auth);
        var tokens = new ChaveUnicaJwtGenerator(
            _secretsManager, Options.Create(new SecretsOptions { ChaveUnicaJwt = "chave-unica-jwt" }), TimeProvider.System);
        var handler = new BffRequestHandler(
            new PosicaoTransportadorService(new EcsPosicaoClient(http, _auth), Options.Create(new PosicaoOptions())),
            centralizadores,
            new ChaveUnicaService(centralizadores, tokens),
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
                    ["transportadora"] = "123"
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
