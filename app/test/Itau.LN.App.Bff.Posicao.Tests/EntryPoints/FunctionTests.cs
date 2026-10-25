using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Itau.LN.App.Bff.Posicao.Lambda;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.EntryPoints;

public class FunctionTests
{
    // Como na Lambda, a Function monta a configuração a partir das env vars.
    public FunctionTests()
    {
        Environment.SetEnvironmentVariable("AWS_REGION", "sa-east-1");
        Environment.SetEnvironmentVariable("ECS_BASE_URL", "https://ecs.local");
        Environment.SetEnvironmentVariable("CENTRALIZADOR_BASE_URL", "https://centralizador.local");
        Environment.SetEnvironmentVariable("AUTH_TOKEN_URL", "https://sts.local/oauth/token");
        Environment.SetEnvironmentVariable("Secrets__ConsumoApi", "consumo-api");
    }

    // Sem tempo restante (0s) a requisição ainda é atendida, só sem prazo de cancelamento.
    [Theory]
    [InlineData(30)]
    [InlineData(0)]
    public async Task FunctionHandler_ComOuSemTempoRestante_EncaminhaParaOHandler(int segundosRestantes)
    {
        var context = Substitute.For<ILambdaContext>();
        context.RemainingTime.Returns(TimeSpan.FromSeconds(segundosRestantes));

        var response = await new Function().FunctionHandler(
            new APIGatewayProxyRequest { HttpMethod = "GET", Path = "/v1/posicao-transportadores" }, context);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Headers obrigatorios ausentes", response.Body);
    }
}
