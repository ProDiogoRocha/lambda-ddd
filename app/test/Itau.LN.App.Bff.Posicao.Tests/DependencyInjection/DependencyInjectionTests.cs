using Itau.LN.App.Bff.Posicao.Lambda;
using Itau.LN.App.Bff.Posicao.Lambda.Handlers;
using Itau.LN.App.Bff.Posicao.Lambda.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itau.LN.App.Bff.Posicao.Tests.DependencyInjection;

public class DependencyInjectionTests
{
    // O client do Secrets Manager exige região; na Lambda AWS_REGION sempre existe.
    public DependencyInjectionTests() => Environment.SetEnvironmentVariable("AWS_REGION", "sa-east-1");

    [Fact]
    public void Build_ComConfiguracaoValida_ResolveHandlerNoEscopo()
    {
        using var provider = DependencyInjectionResolver.Build(Configuration(ConfiguracaoValida()));
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BffRequestHandler>());
    }

    [Fact]
    public void Build_SemConfiguracaoObrigatoria_FalhaNoInit()
    {
        var configuracao = ConfiguracaoValida();
        configuracao.Remove("ECS_BASE_URL");

        var ex = Assert.Throws<OptionsValidationException>(() => DependencyInjectionResolver.Build(Configuration(configuracao)));

        Assert.Contains("EcsBaseUrl", ex.Message);
    }

    [Fact]
    public void Build_LeOrigemDoCorsDaEnv()
    {
        var configuracao = ConfiguracaoValida();
        configuracao["CORS_ALLOWED_ORIGIN"] = "https://front.exemplo.com";

        using var provider = DependencyInjectionResolver.Build(Configuration(configuracao));

        Assert.Equal("https://front.exemplo.com", provider.GetRequiredService<IOptions<BffOptions>>().Value.CorsAllowedOrigin);
    }

    private static Dictionary<string, string?> ConfiguracaoValida() => new()
    {
        ["ECS_BASE_URL"] = "https://ecs.local",
        ["CENTRALIZADOR_BASE_URL"] = "https://centralizador.local",
        ["AUTH_TOKEN_URL"] = "https://sts.local/oauth/token",
        ["Secrets:ConsumoApi"] = "consumo-api"
    };

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
