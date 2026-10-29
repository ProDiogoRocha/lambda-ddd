using System.Net;
using System.Net.Http.Headers;
using NUnit.Framework;

namespace Itau.LN.App.Bff.Posicao.Taac;

// TAAC: testes integrados do BFF de posição pelo API Gateway do ambiente (env vars TAAC_* do testspec).
public class PosicaoApiTests
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly string Centralizador = Env("TAAC_CENTRALIZADOR", "7619");
    private static readonly string Polo = Env("TAAC_POLO", "001");
    private static readonly string Transportadora = Env("TAAC_TRANSPORTADORA", "56");
    private static string _token = string.Empty;

    // Um token STS para a suíte inteira; se falhar, todos os testes caem com o erro do STS no junit.xml.
    [OneTimeSetUp]
    public async Task ObterTokenSts() =>
        _token = await TokenSts.ObterAsync(
            Http,
            Env("TAAC_STS_URL", "https://des-sts-int.mbi.cloud.ihf/api/oauth/token"),
            Env("TAAC_SECRET_NAME", "taac/lambda-ln-app-bff-posicao"));

    [Test]
    public async Task _1_GET_centralizador_200() =>
        await AssertOk($"centralizadores/{Centralizador}");

    [Test]
    public async Task _2_GET_polo_do_centralizador_200() =>
        await AssertOk($"centralizadores/{Centralizador}/polos/{Polo}");

    // Passa pelo centralizador, gera o JWT da chave-unica e consulta o ECS.
    [Test]
    public async Task _3_GET_posicao_transportadores_200() =>
        await AssertOk($"posicao-transportadores?data={Env("TAAC_DATA_MOVIMENTO", DateTime.Now.ToString("yyyy-MM-dd"))}"
            + $"&centralizador={Centralizador}&polo={Polo}&transportadora={Transportadora}");

    // REQUEST com TTL 0 deixa o authorizer decidir entre Unauthorized (401) e policy Deny (403).
    // Em ambos os casos, o Gateway deve negar a chamada e permitir ao navegador ler o erro.
    [TestCase(null)]
    [TestCase("token-sts-invalido")]
    public async Task _4_GET_centralizador_sem_token_valido_nega_acesso(string? token)
    {
        var baseUrl = Env("TAAC_BASE_URL", string.Empty);
        Assert.That(baseUrl, Is.Not.Empty, "TAAC_BASE_URL não definida no testspec.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/centralizadores/{Centralizador}");
        request.Headers.Add("x-itau-apikey", Env("TAAC_API_KEY", "1"));
        request.Headers.Add("x-itau-correlationID", Guid.NewGuid().ToString());
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await Http.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized).Or.EqualTo(HttpStatusCode.Forbidden),
            "O authorizer deve negar a chamada sem token válido.");
        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.True,
            "O erro de autenticação precisa ter CORS para ser lido pelo front.");
    }

    private static async Task AssertOk(string rota)
    {
        var baseUrl = Env("TAAC_BASE_URL", string.Empty);
        Assert.That(baseUrl, Is.Not.Empty, "TAAC_BASE_URL não definida no testspec.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/{rota}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.Add("x-itau-apikey", Env("TAAC_API_KEY", "1"));
        request.Headers.Add("x-itau-correlationID", Guid.NewGuid().ToString());

        using var response = await Http.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
    }

    private static string Env(string nome, string padrao) =>
        Environment.GetEnvironmentVariable(nome) is { Length: > 0 } valor ? valor : padrao;
}
