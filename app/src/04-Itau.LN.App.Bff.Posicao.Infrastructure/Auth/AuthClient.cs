using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SecretsManager;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using Microsoft.Extensions.Options;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Auth;

// Singleton: o token (client_credentials) fica em cache e é renovado pouco antes de expirar.
public sealed class AuthClient(
    IHttpClientFactory httpClientFactory,
    IAmazonSecretsManager secretsManager,
    IOptions<ApiOptions> apiOptions,
    IOptions<SecretsOptions> secretsOptions,
    TimeProvider timeProvider) : IAuthClient
{
    private const int MargemDeRenovacaoEmSegundos = 60;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CachedToken? _token;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (IsValid(_token))
        {
            return _token.AccessToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!IsValid(_token))
            {
                _token = await RequestTokenAsync(cancellationToken);
            }

            return _token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsValid([NotNullWhen(true)] CachedToken? token) =>
        token is not null && token.ExpiresAt > timeProvider.GetUtcNow();

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var secretName = secretsOptions.Value.ConsumoApi;
        var secret = JsonSerializer.Deserialize<ConsumoApiSecret>(
            await secretsManager.GetSecretStringAsync(secretName, cancellationToken))
            ?? throw new InvalidOperationException($"Não foi possível desserializar a secret '{secretName}'.");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = secret.ClientId,
            ["client_secret"] = secret.ClientSecret,
            ["grant_type"] = "client_credentials"
        });

        var stsUrl = apiOptions.Value.AuthTokenUrl;
        using var response = await httpClientFactory.CreateClient().PostAsync(stsUrl, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // O corpo de erro do OAuth (ex.: invalid_client) diz o motivo e não traz segredo.
            throw new HttpRequestException(
                $"STS recusou o token em {stsUrl}: HTTP {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}",
                null,
                response.StatusCode);
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            throw new InvalidOperationException($"STS respondeu sem access_token em {stsUrl}.");
        }

        // Renova 60s antes de expirar; com expires_in curto, na metade da validade.
        var validadeEmSegundos = Math.Max(token.ExpiresIn - MargemDeRenovacaoEmSegundos, token.ExpiresIn / 2.0);
        return new CachedToken(token.AccessToken, timeProvider.GetUtcNow().AddSeconds(validadeEmSegundos));
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record ConsumoApiSecret(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_secret")] string ClientSecret);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
