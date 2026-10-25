using System.Text.Json;
using Itau.LN.App.Bff.Posicao.Shared.Helpers;

namespace Itau.LN.App.Bff.Posicao.Tests.Helpers;

public class JsonHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{invalido")]
    public void DeserializeOrDefault_ComJsonAusenteOuInvalido_RetornaDefault(string? json)
    {
        Assert.Null(JsonHelper.DeserializeOrDefault<Exemplo>(json));
    }

    [Fact]
    public void DeserializeOrDefault_IgnoraCaixaEAceitaNumeroComoString()
    {
        Assert.Equal(new Exemplo("a", 10.5m), JsonHelper.DeserializeOrDefault<Exemplo>("""{"NOME":"a","valor":"10.5"}"""));
    }

    [Fact]
    public void Options_SerializaEmCamelCase()
    {
        Assert.Equal("""{"nome":"a","valor":1}""", JsonSerializer.Serialize(new Exemplo("a", 1m), JsonHelper.Options));
    }

    private sealed record Exemplo(string Nome, decimal Valor);
}
