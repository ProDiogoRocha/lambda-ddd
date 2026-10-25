using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Itau.LN.App.Bff.Posicao.Lambda.Handlers;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Itau.LN.App.Bff.Posicao.Lambda;

public class Function
{
    // Folga para devolver a resposta antes do timeout da Lambda.
    private static readonly TimeSpan MargemDeEncerramento = TimeSpan.FromSeconds(1);

    private readonly ServiceProvider _provider = DependencyInjectionResolver.Build();

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
