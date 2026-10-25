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

// Handler, serviço e client HTTP reais: só o banco, o token e o backend são falsos.
public class ChaveUnicaPontaAPontaTests
{
    [Fact]
    public async Task ChaveDoRds_ChegaNoHeaderDaRequisicaoAoBackend()
    {
        var credenciais = Substitute.For<ICredenciaisRepository>();
        credenciais.ObterChaveUnicaAsync("TRANSP", Arg.Any<CancellationToken>()).Returns("chave-do-rds");
        var auth = Substitute.For<IAuthClient>();
        auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns("token");
        var backend = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(
            HttpStatusCode.OK, """{"data":{"numeroPosicao":"1","justificativaFechamento":"termo"}}"""));
        var ecs = new EcsPosicaoClient(new HttpClient(backend) { BaseAddress = new Uri("https://ecs.local/") }, auth);
        var handler = new BffRequestHandler(
            new PosicaoTransportadorService(ecs, Options.Create(new PosicaoOptions())),
            Substitute.For<ICentralizadoresClient>(),
            credenciais,
            Options.Create(new BffOptions()));

        var response = await handler.HandleAsync(
            new APIGatewayProxyRequest
            {
                HttpMethod = "GET",
                Path = "/v1/posicao-transportadores/IDX/justificativa-termo-juridico",
                QueryStringParameters = new Dictionary<string, string> { ["transportadora"] = "TRANSP" },
                Headers = new Dictionary<string, string>
                {
                    ["x-itau-apikey"] = "api-key",
                    ["x-itau-correlationID"] = "corr-id",
                    ["chave-unica"] = "chave-do-header"
                }
            },
            Substitute.For<ILambdaContext>(),
            CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("chave-do-rds", Assert.Single(backend.Requests).Headers.GetValues("chave-unica").Single());
    }
}
