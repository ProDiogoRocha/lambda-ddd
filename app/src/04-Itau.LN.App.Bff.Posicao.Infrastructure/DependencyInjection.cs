using Amazon.SecretsManager;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Clients;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itau.LN.App.Bff.Posicao.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiOptions>().Bind(configuration).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SecretsOptions>().Bind(configuration.GetSection("Secrets")).ValidateDataAnnotations().ValidateOnStart();
        // Sem ValidateOnStart: só as rotas de posição assinam a chave-unica; sem certificado os centralizadores seguem funcionando.
        services.AddOptions<ChaveUnicaOptions>().Bind(configuration);

        services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAuthClient, AuthClient>();
        services.AddSingleton<IChaveUnicaTokenGenerator, ChaveUnicaJwtGenerator>();

        services.AddHttpClient<IEcsPosicaoClient, EcsPosicaoClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ApiOptions>>().Value;
            client.BaseAddress = BaseAddress(options.EcsBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.EcsTimeoutSeconds);
        });

        services.AddHttpClient<ICentralizadoresClient, CentralizadoresClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ApiOptions>>().Value;
            client.BaseAddress = BaseAddress(options.CentralizadorBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.CentralizadorTimeoutSeconds);
        });

        return services;
    }

    // Sem a barra final, o HttpClient descarta o último segmento da URL base ao combinar com o endpoint relativo.
    private static Uri BaseAddress(string url) => new(url.TrimEnd('/') + "/");
}
