using System.Text.Json;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace Itau.LN.App.Bff.Posicao.Taac;

// Token STS (client_credentials) com o client_id/client_secret da secret do TAAC, como no TokenSTS da lambda de referência.
internal static class TokenSts
{
    public static async Task<string> ObterAsync(HttpClient http, string stsUrl, string secretName)
    {
        using var secretsManager = new AmazonSecretsManagerClient(RegionEndpoint.SAEast1);
        var secret = await secretsManager.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });
        using var credenciais = JsonDocument.Parse(secret.SecretString);

        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = credenciais.RootElement.GetProperty("client_id").GetString()!,
            ["client_secret"] = credenciais.RootElement.GetProperty("client_secret").GetString()!
        });
        using var resposta = await http.PostAsync(stsUrl, formulario);
        resposta.EnsureSuccessStatusCode();

        using var token = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return token.RootElement.GetProperty("access_token").GetString()!;
    }
}
