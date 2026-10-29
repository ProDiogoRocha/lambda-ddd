using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Options;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Auth;

public class ChaveUnicaJwtGeneratorTests
{
    private static readonly DateTimeOffset Agora = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();
    private readonly TimeProvider _relogio = Substitute.For<TimeProvider>();

    public ChaveUnicaJwtGeneratorTests()
    {
        _secretsManager
            .GetSecretValueAsync(Arg.Is<GetSecretValueRequest>(request => request.SecretId == "chave-unica-jwt"), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse { SecretString = "segredo-de-teste" });
        _relogio.GetUtcNow().Returns(Agora);
    }

    [Fact]
    public async Task Gerar_AssinaEmHs256ComTransportadoraFilialEValidadeDe5Minutos()
    {
        var jwt = await Gerador("chave-unica-jwt").GerarAsync("123", "B1", CancellationToken.None);

        var partes = jwt.Split('.');
        Assert.Equal("""{"alg":"HS256","typ":"JWT"}""", Decodificar(partes[0]));
        // Claims e tipos iguais aos do token que o ECS já aceita: codigo_transportadora e codigo_filial em texto.
        Assert.Equal("""{"codigo_transportadora":"123","codigo_filial":"B1","iat":1800000000,"exp":1800000300}""", Decodificar(partes[1]));
        Assert.Equal(
            HMACSHA256.HashData("segredo-de-teste"u8, Encoding.ASCII.GetBytes($"{partes[0]}.{partes[1]}")),
            Base64Url.DecodeFromChars(partes[2]));
    }

    [Fact]
    public async Task Gerar_LeASecretUmaVezSoPorContainer()
    {
        var gerador = Gerador("chave-unica-jwt");

        await gerador.GerarAsync("123", "B1", CancellationToken.None);
        await gerador.GerarAsync("456", "B2", CancellationToken.None);

        await _secretsManager.ReceivedWithAnyArgs(1).GetSecretValueAsync(default!, default);
    }

    [Fact]
    public async Task Gerar_SemSecretConfigurada_FalhaSemChamarOSecretsManager()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Gerador(string.Empty).GerarAsync("123", "B1", CancellationToken.None));

        Assert.Contains("Secrets__ChaveUnicaJwt", ex.Message);
        Assert.Empty(_secretsManager.ReceivedCalls());
    }

    private ChaveUnicaJwtGenerator Gerador(string secretName) =>
        new(_secretsManager, Options.Create(new SecretsOptions { ChaveUnicaJwt = secretName }), _relogio);

    private static string Decodificar(string parte) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parte));
}
