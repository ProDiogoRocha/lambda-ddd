using System.Text.Json;
using Amazon.SecretsManager;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;

// Singleton: monta a connection string a partir das secrets do RDS uma vez por container.
// ponytail: senha rotacionada só é lida no próximo cold start; limpar o cache em erro de login se houver rotação automática.
public sealed class RdsConnectionProvider(IAmazonSecretsManager secretsManager, IOptions<RdsOptions> options)
{
    private string? _connectionString;

    public async Task<string> GetConnectionStringAsync(CancellationToken cancellationToken) =>
        _connectionString ??= await BuildConnectionStringAsync(cancellationToken);

    private async Task<string> BuildConnectionStringAsync(CancellationToken cancellationToken)
    {
        var rds = options.Value;

        return new MySqlConnectionStringBuilder
        {
            Server = await GetSecretValueAsync(rds.EndpointSecret, ["host", "endpoint"], cancellationToken),
            UserID = await GetSecretValueAsync(rds.UserSecret, ["username", "user"], cancellationToken),
            Password = await GetSecretValueAsync(rds.PasswordSecret, ["password"], cancellationToken),
            Database = rds.Database,
            Port = (uint)rds.Port
        }.ConnectionString;
    }

    // A secret pode ser texto puro ou JSON; no JSON vale a primeira chave candidata encontrada.
    private async Task<string> GetSecretValueAsync(string secretName, string[] jsonKeyCandidates, CancellationToken cancellationToken)
    {
        var raw = await secretsManager.GetSecretStringAsync(secretName, cancellationToken);
        if (!raw.StartsWith('{'))
        {
            return raw;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            foreach (var key in jsonKeyCandidates)
            {
                if (document.RootElement.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            return raw;
        }

        throw new InvalidOperationException($"Não foi possível localizar os campos esperados na secret '{secretName}'.");
    }
}
