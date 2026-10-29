using System.Net;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Tests.Fakes;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Auth;

public class AuthClientTests
{
    private const string TokenValido = """{"access_token":"abc","expires_in":120}""";

    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();
    private readonly RelogioManual _relogio = new();

    public AuthClientTests() => Secret("""{"client_id":"id","client_secret":"segredo"}""");

    [Fact]
    public async Task GetToken_ReusaTokenEmCacheERenovaAntesDeExpirar()
    {
        var http = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, TokenValido);
        var auth = CriarAuthClient(http);

        Assert.Equal("abc", await auth.GetTokenAsync(CancellationToken.None));
        _relogio.Agora += TimeSpan.FromSeconds(59);
        await auth.GetTokenAsync(CancellationToken.None);

        var request = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://sts.local/oauth/token", request.RequestUri!.AbsoluteUri);
        Assert.Equal("client_id=id&client_secret=segredo&grant_type=client_credentials", Assert.Single(http.Bodies));

        // expires_in 120 => renova 60s antes: aos 60s já busca outro token.
        _relogio.Agora += TimeSpan.FromSeconds(1);
        await auth.GetTokenAsync(CancellationToken.None);

        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task GetToken_ComExpiracaoCurta_RenovaNaMetadeDaValidade()
    {
        var http = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, """{"access_token":"abc","expires_in":40}""");
        var auth = CriarAuthClient(http);

        await auth.GetTokenAsync(CancellationToken.None);
        _relogio.Agora += TimeSpan.FromSeconds(19);
        await auth.GetTokenAsync(CancellationToken.None);
        Assert.Single(http.Requests);

        _relogio.Agora += TimeSpan.FromSeconds(2);
        await auth.GetTokenAsync(CancellationToken.None);
        Assert.Equal(2, http.Requests.Count);
    }

    // As 4 consultas de detalhe em paralelo podem pedir token ao mesmo tempo: só uma vai ao STS.
    [Fact]
    public async Task GetToken_ChamadasSimultaneas_BuscamUmUnicoToken()
    {
        var http = new FakeHttpMessageHandler(_ =>
        {
            Thread.Sleep(50);
            return FakeHttpMessageHandler.Json(HttpStatusCode.OK, TokenValido);
        });
        var auth = CriarAuthClient(http);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => auth.GetTokenAsync(CancellationToken.None))));

        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task GetToken_ComSecretNula_Falha()
    {
        Secret("null");
        var auth = CriarAuthClient(FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, TokenValido));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => auth.GetTokenAsync(CancellationToken.None));

        Assert.Equal("Não foi possível desserializar a secret 'consumo-api'.", ex.Message);
    }

    [Theory]
    [InlineData("""{"access_token":" ","expires_in":120}""")]
    [InlineData("""{"expires_in":120}""")]
    [InlineData("null")]
    public async Task GetToken_SemAccessToken_Falha(string resposta)
    {
        var auth = CriarAuthClient(FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, resposta));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => auth.GetTokenAsync(CancellationToken.None));

        Assert.Equal("STS respondeu sem access_token em https://sts.local/oauth/token.", ex.Message);
    }

    [Fact]
    public async Task GetToken_ComErroNoSts_PropagaEPermiteNovaTentativa()
    {
        var respostas = new Queue<HttpResponseMessage>([
            FakeHttpMessageHandler.Json(HttpStatusCode.Unauthorized, "{}"),
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, TokenValido)]);
        var auth = CriarAuthClient(new FakeHttpMessageHandler(_ => respostas.Dequeue()));

        await Assert.ThrowsAsync<HttpRequestException>(() => auth.GetTokenAsync(CancellationToken.None));

        Assert.Equal("abc", await auth.GetTokenAsync(CancellationToken.None));
    }

    private AuthClient CriarAuthClient(FakeHttpMessageHandler http)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(http));

        return new AuthClient(
            factory,
            _secretsManager,
            Options.Create(new ApiOptions { AuthTokenUrl = "https://sts.local/oauth/token" }),
            Options.Create(new SecretsOptions { ConsumoApi = "consumo-api" }),
            _relogio);
    }

    private void Secret(string value) =>
        _secretsManager
            .GetSecretValueAsync(Arg.Is<GetSecretValueRequest>(request => request.SecretId == "consumo-api"), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse { SecretString = value });

    private sealed class RelogioManual : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
