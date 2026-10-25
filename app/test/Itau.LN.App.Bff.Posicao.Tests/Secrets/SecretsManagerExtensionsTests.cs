using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Secrets;

public class SecretsManagerExtensionsTests
{
    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();

    [Fact]
    public async Task GetSecretString_RemoveEspacosDasPontas()
    {
        Secret("  valor\n");

        Assert.Equal("valor", await _secretsManager.GetSecretStringAsync("minha-secret", CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSecretString_SemConteudo_Falha(string? valor)
    {
        Secret(valor);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _secretsManager.GetSecretStringAsync("minha-secret", CancellationToken.None));

        Assert.Equal("Secret 'minha-secret' não possui conteúdo.", ex.Message);
    }

    private void Secret(string? value) =>
        _secretsManager
            .GetSecretValueAsync(Arg.Is<GetSecretValueRequest>(request => request.SecretId == "minha-secret"), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse { SecretString = value });
}
