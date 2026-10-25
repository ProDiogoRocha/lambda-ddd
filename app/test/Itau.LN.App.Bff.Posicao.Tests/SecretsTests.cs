using System.Net;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests;

public class SecretsTests
{
    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();

    [Fact]
    public async Task AuthClient_ReusaTokenEmCacheERenovaAntesDeExpirar()
    {
        Secret("consumo-api", """{"client_id":"id","client_secret":"segredo"}""");
        var http = new FakeHttpMessageHandler(
            _ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"access_token":"abc","expires_in":120}"""));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(http));
        var relogio = new RelogioManual();
        var auth = new AuthClient(
            factory,
            _secretsManager,
            Options.Create(new ApiOptions { AuthTokenUrl = "https://sts.local/oauth/token" }),
            Options.Create(new SecretsOptions { ConsumoApi = "consumo-api" }),
            relogio);

        Assert.Equal("abc", await auth.GetTokenAsync(CancellationToken.None));
        relogio.Agora += TimeSpan.FromSeconds(59);
        await auth.GetTokenAsync(CancellationToken.None);

        Assert.Equal("https://sts.local/oauth/token", Assert.Single(http.Requests).RequestUri!.AbsoluteUri);
        Assert.Equal("client_id=id&client_secret=segredo&grant_type=client_credentials", Assert.Single(http.Bodies));

        // expires_in 120 => renova 60s antes: aos 61s já busca outro token.
        relogio.Agora += TimeSpan.FromSeconds(2);
        await auth.GetTokenAsync(CancellationToken.None);

        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task RdsConnectionProvider_AceitaSecretEmTextoPuroOuJsonEFazCache()
    {
        Secret("rds-host", """{"host":"db.local"}""");
        Secret("rds-user", "admin");
        Secret("rds-password", """{"password":"p@ss"}""");
        var provider = new RdsConnectionProvider(_secretsManager, Options.Create(new RdsOptions
        {
            EndpointSecret = "rds-host",
            UserSecret = "rds-user",
            PasswordSecret = "rds-password",
            Database = "numerario"
        }));

        var connection = new MySqlConnectionStringBuilder(await provider.GetConnectionStringAsync(CancellationToken.None));
        await provider.GetConnectionStringAsync(CancellationToken.None);

        Assert.Equal("db.local", connection.Server);
        Assert.Equal("admin", connection.UserID);
        Assert.Equal("p@ss", connection.Password);
        Assert.Equal("numerario", connection.Database);
        Assert.Equal(3306u, connection.Port);
        await _secretsManager.ReceivedWithAnyArgs(3).GetSecretValueAsync(default!, default);
    }

    [Fact]
    public async Task RdsConnectionProvider_JsonSemChaveEsperada_Falha()
    {
        Secret("rds-host", """{"outro":"valor"}""");
        var provider = new RdsConnectionProvider(_secretsManager, Options.Create(new RdsOptions { EndpointSecret = "rds-host" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetConnectionStringAsync(CancellationToken.None));
    }

    private sealed class RelogioManual : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private void Secret(string name, string value) =>
        _secretsManager
            .GetSecretValueAsync(Arg.Is<GetSecretValueRequest>(request => request.SecretId == name), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse { SecretString = value });
}
