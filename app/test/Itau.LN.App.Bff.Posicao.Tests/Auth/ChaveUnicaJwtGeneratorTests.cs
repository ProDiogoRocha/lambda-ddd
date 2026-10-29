using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Auth;

public sealed class ChaveUnicaJwtGeneratorTests : IDisposable
{
    private static readonly DateTimeOffset Agora = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    // CRLF de propósito: a chave são os bytes exatos do arquivo, sem normalizar quebras de linha.
    private static readonly byte[] Bundle = Encoding.ASCII.GetBytes("-----BEGIN CERTIFICATE-----\r\nMIIB\r\n-----END CERTIFICATE-----\r\n");

    private readonly string _arquivo = Path.GetTempFileName();
    private readonly TimeProvider _relogio = Substitute.For<TimeProvider>();

    public ChaveUnicaJwtGeneratorTests()
    {
        File.WriteAllBytes(_arquivo, Bundle);
        _relogio.GetUtcNow().Returns(Agora);
    }

    public void Dispose() => File.Delete(_arquivo);

    // O ECS valida com o mesmo ca_bundle.crt: HMAC-SHA256 com os bytes do arquivo.
    [Fact]
    public async Task Gerar_AssinaEmHs256ComOsBytesDoArquivo()
    {
        var partes = (await Gerador(_arquivo).GerarAsync("123", "B1", CancellationToken.None)).Split('.');

        Assert.Equal("""{"alg":"HS256","typ":"JWT"}""", Decodificar(partes[0]));
        // Claims e tipos iguais aos do token que o ECS já aceita: codigo_transportadora e codigo_filial em texto.
        Assert.Equal("""{"codigo_transportadora":"123","codigo_filial":"B1","iat":1800000000,"exp":1800000300}""", Decodificar(partes[1]));
        Assert.Equal(
            HMACSHA256.HashData(Bundle, Encoding.ASCII.GetBytes($"{partes[0]}.{partes[1]}")),
            Base64Url.DecodeFromChars(partes[2]));
    }

    // O arquivo é lido uma vez por container: apagar depois da primeira assinatura não afeta as seguintes.
    [Fact]
    public async Task Gerar_LeOArquivoUmaVezSo()
    {
        var gerador = Gerador(_arquivo);
        await gerador.GerarAsync("123", "B1", CancellationToken.None);

        File.Delete(_arquivo);

        Assert.NotEmpty(await gerador.GerarAsync("456", "B2", CancellationToken.None));
    }

    [Fact]
    public async Task Gerar_SemOArquivo_FalhaDizendoOndeColocar()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Gerador("Certificados/nao-existe.crt").GerarAsync("123", "B1", CancellationToken.None));

        Assert.Contains("nao-existe.crt", ex.Message);
        Assert.Contains("pasta Certificados", ex.Message);
    }

    [Fact]
    public async Task Gerar_ArquivoVazio_FalhaEmVezDeAssinarComChaveVazia()
    {
        await File.WriteAllBytesAsync(_arquivo, []);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Gerador(_arquivo).GerarAsync("123", "B1", CancellationToken.None));

        Assert.Contains("vazio", ex.Message);
    }

    private ChaveUnicaJwtGenerator Gerador(string caminho) =>
        new(Options.Create(new ChaveUnicaOptions { Certificado = caminho }), _relogio);

    private static string Decodificar(string parte) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parte));
}
