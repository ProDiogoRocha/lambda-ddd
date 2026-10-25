using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Secrets;

public class RdsConnectionProviderTests
{
    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();

    // "host" não-string é ignorado e vale a chave alternativa "endpoint".
    [Fact]
    public async Task GetConnectionString_AceitaSecretEmTextoPuroOuJsonEFazCache()
    {
        Secret("rds-host", """{"host":123,"endpoint":"db.local"}""");
        Secret("rds-user", "admin");
        Secret("rds-password", """{"password":"p@ss"}""");
        var provider = new RdsConnectionProvider(_secretsManager, Options.Create(new RdsOptions
        {
            EndpointSecret = "rds-host",
            UserSecret = "rds-user",
            PasswordSecret = "rds-password",
            Database = "numerario",
            Port = 3307
        }));

        var connection = new MySqlConnectionStringBuilder(await provider.GetConnectionStringAsync(CancellationToken.None));
        await provider.GetConnectionStringAsync(CancellationToken.None);

        Assert.Equal("db.local", connection.Server);
        Assert.Equal("admin", connection.UserID);
        Assert.Equal("p@ss", connection.Password);
        Assert.Equal("numerario", connection.Database);
        Assert.Equal(3307u, connection.Port);
        await _secretsManager.ReceivedWithAnyArgs(3).GetSecretValueAsync(default!, default);
    }

    [Fact]
    public async Task GetConnectionString_JsonSemChaveEsperada_Falha()
    {
        Secret("rds-host", """{"outro":"valor"}""");
        var provider = new RdsConnectionProvider(_secretsManager, Options.Create(new RdsOptions { EndpointSecret = "rds-host" }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetConnectionStringAsync(CancellationToken.None));

        Assert.Equal("Não foi possível localizar os campos esperados na secret 'rds-host'.", ex.Message);
    }

    [Fact]
    public async Task GetConnectionString_ComTextoIniciadoPorAbreChaves_UsaTextoPuro()
    {
        Secret("rds-host", "db.local");
        Secret("rds-user", """{"user":"admin"}""");
        Secret("rds-password", "{senha-sem-json");
        var provider = new RdsConnectionProvider(_secretsManager, Options.Create(new RdsOptions
        {
            EndpointSecret = "rds-host",
            UserSecret = "rds-user",
            PasswordSecret = "rds-password"
        }));

        var connection = new MySqlConnectionStringBuilder(await provider.GetConnectionStringAsync(CancellationToken.None));

        Assert.Equal("{senha-sem-json", connection.Password);
        Assert.Equal("admin", connection.UserID);
    }

    private void Secret(string name, string value) =>
        _secretsManager
            .GetSecretValueAsync(Arg.Is<GetSecretValueRequest>(request => request.SecretId == name), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse { SecretString = value });
}
