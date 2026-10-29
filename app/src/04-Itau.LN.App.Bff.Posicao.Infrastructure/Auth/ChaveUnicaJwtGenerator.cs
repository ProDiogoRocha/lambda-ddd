using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Auth;

// Singleton: a chave-unica do ECS é um JWT HS256 cuja chave são os bytes do ca_bundle.crt empacotado na Lambda
// (pasta Certificados do projeto Lambda); o ECS valida com o mesmo arquivo. Decisão do tech lead em 29/10/2026.
// O ca_bundle.crt é público na organização: quem tiver o arquivo consegue gerar uma chave-unica válida.
// Claims em snake_case e valores em texto, como no token que o ECS já aceita; iat/exp limitam o reuso a 5 minutos.
public sealed class ChaveUnicaJwtGenerator(IOptions<ChaveUnicaOptions> options, TimeProvider timeProvider) : IChaveUnicaTokenGenerator
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private static readonly string Header = Base64Url.EncodeToString("""{"alg":"HS256","typ":"JWT"}"""u8);
    private byte[]? _chave;

    public Task<string> GerarAsync(string codigoTransportadora, string codigoFilial, CancellationToken cancellationToken)
    {
        _chave ??= LerChave(options.Value);

        var agora = timeProvider.GetUtcNow();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["codigo_transportadora"] = codigoTransportadora,
            ["codigo_filial"] = codigoFilial,
            ["iat"] = agora.ToUnixTimeSeconds(),
            ["exp"] = agora.Add(Validade).ToUnixTimeSeconds()
        });

        var conteudo = $"{Header}.{Base64Url.EncodeToString(payload)}";
        var assinatura = HMACSHA256.HashData(_chave, Encoding.ASCII.GetBytes(conteudo));

        return Task.FromResult($"{conteudo}.{Base64Url.EncodeToString(assinatura)}");
    }

    // Bytes exatos do arquivo, sem trim nem conversão de texto: qualquer diferença com o arquivo do ECS invalida a assinatura.
    private static byte[] LerChave(ChaveUnicaOptions opcoes)
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, opcoes.Certificado);
        if (!File.Exists(caminho))
        {
            throw new InvalidOperationException(
                $"Certificado da chave-unica não encontrado em {caminho}: coloque o ca_bundle.crt na pasta Certificados do projeto Lambda.");
        }

        return File.ReadAllBytes(caminho) is { Length: > 0 } chave
            ? chave
            : throw new InvalidOperationException($"Certificado da chave-unica vazio em {caminho}: uma chave vazia não protege a assinatura.");
    }
}
