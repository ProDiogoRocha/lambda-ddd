using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Infrastructure.Repositories;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Itau.LN.App.Bff.Posicao.Tests.Repositories;

// Sem banco: o SELECT em si só se valida contra um MySQL real (teste de integração).
public class CredenciaisRepositoryTests
{
    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();

    [Fact]
    public async Task ObterChaveUnica_ComFalhaNasSecrets_PropagaOErro()
    {
        _secretsManager.GetSecretValueAsync(default!, default).ThrowsAsyncForAnyArgs(new InvalidOperationException("secret fora"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Repositorio().ObterChaveUnicaAsync("TRANSP", CancellationToken.None));

        Assert.Equal("secret fora", ex.Message);
    }

    // O cancelamento da Lambda chega ao banco: a consulta é abortada antes de conectar.
    [Fact]
    public async Task ObterChaveUnica_ComRequisicaoCancelada_AbortaAConsulta()
    {
        _secretsManager.GetSecretValueAsync(default!, default).ReturnsForAnyArgs(new GetSecretValueResponse { SecretString = "valor" });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Repositorio().ObterChaveUnicaAsync("TRANSP", new CancellationToken(canceled: true)));
    }

    // Porta 1: nada escuta nela, então nenhum MySQL local é tocado.
    private CredenciaisRepository Repositorio() =>
        new(new RdsConnectionProvider(_secretsManager, Options.Create(new RdsOptions { Port = 1 })));
}
