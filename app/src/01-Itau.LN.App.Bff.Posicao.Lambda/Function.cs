using System.Security.Cryptography;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Lambda.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Itau.LN.App.Bff.Posicao.Lambda;

public class Function
{
    // Folga para devolver a resposta antes do timeout da Lambda.
    private static readonly TimeSpan MargemDeEncerramento = TimeSpan.FromSeconds(1);

    private readonly ServiceProvider _provider;

    // Cold start: a configuração já foi validada no Build; o log mostra o que este ambiente carregou.
    public Function()
    {
        _provider = DependencyInjectionResolver.Build();

        var api = _provider.GetRequiredService<IOptions<ApiOptions>>().Value;
        var secrets = _provider.GetRequiredService<IOptions<SecretsOptions>>().Value;
        var certificado = Path.Combine(
            AppContext.BaseDirectory, _provider.GetRequiredService<IOptions<ChaveUnicaOptions>>().Value.Certificado);
        // O SHA-256 identifica o arquivo sem expor o conteúdo: tem que ser igual ao do arquivo usado pelo ECS.
        LambdaLogger.Log(
            LogLevel.Information,
            "BFF iniciado | ecs={EcsBaseUrl} centralizador={CentralizadorBaseUrl} sts={AuthTokenUrl} secretConsumoApi={SecretConsumoApi} certificadoChaveUnica={CertificadoChaveUnica} certificadoSha256={CertificadoSha256}",
            api.EcsBaseUrl,
            api.CentralizadorBaseUrl,
            api.AuthTokenUrl,
            secrets.ConsumoApi,
            certificado,
            File.Exists(certificado) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(certificado))) : "ausente");
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var tempoDisponivel = context.RemainingTime - MargemDeEncerramento;
        using var cancellation = tempoDisponivel > TimeSpan.Zero
            ? new CancellationTokenSource(tempoDisponivel)
            : new CancellationTokenSource();

        // Um escopo por invocação: serviços Scoped não sobrevivem entre requisições.
        await using var scope = _provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<BffRequestHandler>();

        return await handler.HandleAsync(request, context, cancellation.Token);
    }
}
