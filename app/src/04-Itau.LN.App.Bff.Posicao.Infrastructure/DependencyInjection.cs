using Amazon.SecretsManager;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Clients;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Infrastructure.Repositories;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
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
        // Sem ValidateOnStart: só as rotas de posição usam o RDS.
        services.AddOptions<RdsOptions>().Bind(configuration.GetSection("RdsConfig")).ValidateDataAnnotations();

        services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAuthClient, AuthClient>();
        services.AddSingleton<RdsConnectionProvider>();
        services.AddScoped<ICredenciaisRepository, CredenciaisRepository>();

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
