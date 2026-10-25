using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;

public static class SecretsManagerExtensions
{
    public static async Task<string> GetSecretStringAsync(
        this IAmazonSecretsManager secretsManager, string secretName, CancellationToken cancellationToken)
    {
        var response = await secretsManager.GetSecretValueAsync(
            new GetSecretValueRequest { SecretId = secretName }, cancellationToken);

        return string.IsNullOrWhiteSpace(response.SecretString)
            ? throw new InvalidOperationException($"Secret '{secretName}' não possui conteúdo.")
            : response.SecretString.Trim();
    }
}
