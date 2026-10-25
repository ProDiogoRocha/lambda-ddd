using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Infrastructure.Repositories;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Repositories;

// Sem banco: o SELECT em si só se valida contra um MySQL real (teste de integração).
public class CredenciaisRepositoryTests
{
    // O cancelamento da Lambda chega ao banco: a consulta é abortada antes de conectar.
    [Fact]
    public async Task ObterChaveUnica_ComRequisicaoCancelada_AbortaAConsulta()
    {
        var secretsManager = Substitute.For<IAmazonSecretsManager>();
        secretsManager.GetSecretValueAsync(default!, default).ReturnsForAnyArgs(new GetSecretValueResponse { SecretString = "127.0.0.1" });
        // Host local e porta 1: se a conexão chegar a ser tentada, falha na hora sem alcançar banco real.
        var repository = new CredenciaisRepository(
            new RdsConnectionProvider(secretsManager, Options.Create(new RdsOptions { Port = 1 })));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.ObterChaveUnicaAsync("TRANSP", new CancellationToken(canceled: true)));
    }
}
