using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.SecretsManager;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using Microsoft.Extensions.Options;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Auth;

// Singleton: a chave-unica do ECS é um JWT HS256 com a transportadora e a filial, no formato do token que o ECS já aceita
// (claims em snake_case, valores em texto). iat/exp a mais limitam o reuso de um token vazado a 5 minutos.
// HS256 = assinatura com segredo compartilhado com o ECS (sem certificado); o segredo vem do Secrets Manager e fica em cache.
public sealed class ChaveUnicaJwtGenerator(
    IAmazonSecretsManager secretsManager,
    IOptions<SecretsOptions> secretsOptions,
    TimeProvider timeProvider) : IChaveUnicaTokenGenerator
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private static readonly string Header = Base64Url.EncodeToString("""{"alg":"HS256","typ":"JWT"}"""u8);
    private byte[]? _chaveDeAssinatura;

    public async Task<string> GerarAsync(string codigoTransportadora, string codigoFilial, CancellationToken cancellationToken)
    {
        _chaveDeAssinatura ??= await ObterChaveDeAssinaturaAsync(cancellationToken);

        var agora = timeProvider.GetUtcNow();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["codigo_transportadora"] = codigoTransportadora,
            ["codigo_filial"] = codigoFilial,
            ["iat"] = agora.ToUnixTimeSeconds(),
            ["exp"] = agora.Add(Validade).ToUnixTimeSeconds()
        });

        var conteudo = $"{Header}.{Base64Url.EncodeToString(payload)}";
        var assinatura = HMACSHA256.HashData(_chaveDeAssinatura, Encoding.ASCII.GetBytes(conteudo));

        return $"{conteudo}.{Base64Url.EncodeToString(assinatura)}";
    }

    private async Task<byte[]> ObterChaveDeAssinaturaAsync(CancellationToken cancellationToken)
    {
        var secretName = secretsOptions.Value.ChaveUnicaJwt;
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new InvalidOperationException("Secret de assinatura da chave-unica não configurada (Secrets__ChaveUnicaJwt).");
        }

        return Encoding.UTF8.GetBytes(await secretsManager.GetSecretStringAsync(secretName, cancellationToken));
    }
}
