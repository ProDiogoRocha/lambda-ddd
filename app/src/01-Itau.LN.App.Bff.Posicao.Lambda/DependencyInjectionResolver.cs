using Itau.LN.App.Bff.Posicao.Application.Options;
using Itau.LN.App.Bff.Posicao.Application.Services;
using Itau.LN.App.Bff.Posicao.Infrastructure;
using Itau.LN.App.Bff.Posicao.Lambda.Handlers;
using Itau.LN.App.Bff.Posicao.Lambda.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itau.LN.App.Bff.Posicao.Lambda;

public static class DependencyInjectionResolver
{
    public static ServiceProvider Build() =>
        Build(new ConfigurationBuilder().AddEnvironmentVariables().Build());

    public static ServiceProvider Build(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);
        services.AddOptions<PosicaoOptions>().Bind(configuration);
        services.AddOptions<BffOptions>().Bind(configuration);
        services.AddScoped<PosicaoTransportadorService>();
        services.AddScoped<BffRequestHandler>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Falha no init da Lambda se faltar configuração obrigatória (ValidateOnStart).
        provider.GetRequiredService<IStartupValidator>().Validate();

        return provider;
    }
}
