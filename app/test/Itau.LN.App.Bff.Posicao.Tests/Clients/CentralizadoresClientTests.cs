using System.Net;
using System.Text.Json;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Infrastructure.Clients;
using Itau.LN.App.Bff.Posicao.Shared.Helpers;
using Itau.LN.App.Bff.Posicao.Tests.Fakes;
using NSubstitute;

namespace Itau.LN.App.Bff.Posicao.Tests.Clients;

public class CentralizadoresClientTests
{
    private static readonly ApiRequestHeaders Headers = new("api-key", "corr-id", "chave");

    private readonly IAuthClient _auth = Substitute.For<IAuthClient>();

    public CentralizadoresClientTests() => _auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns("token-123");

    [Theory]
    [InlineData("centralizador", "https://api.local/base/centralizadores/10")]
    [InlineData("fornecedor", "https://api.local/base/centralizadores/10/fornecedores/F%201")]
    [InlineData("base", "https://api.local/base/centralizadores/10/fornecedores/F%201/bases/B%3F2")]
    [InlineData("polo", "https://api.local/base/centralizadores/10/polos/P%233")]
    public async Task Rotas_EscapamCadaSegmento(string rota, string urlEsperada)
    {
        var http = FakeHttpMessageHandler.Respondendo(HttpStatusCode.OK, """{"data":{"totalRegistros":"0"}}""");
        var client = new CentralizadoresClient(CriarHttpClient(http), _auth);

        var response = await (rota switch
        {
            "centralizador" => client.GetCentralizadorAsync("10", Headers, CancellationToken.None),
            "fornecedor" => client.GetFornecedorAsync("10", "F 1", Headers, CancellationToken.None),
            "base" => client.GetFornecedorInBaseAsync("10", "F 1", "B?2", Headers, CancellationToken.None),
            "polo" => client.GetPoloAsync("10", "P#3", Headers, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(rota))
        });

        Assert.Equal(urlEsperada, Assert.Single(http.Requests).RequestUri!.AbsoluteUri);
        Assert.Equal("0", response.TotalRegistros);
    }

    // O BFF repassa o registro como vem da API: todos os campos voltam com o mesmo nome e valor.
    [Fact]
    public async Task GetCentralizador_RepassaTodosOsCamposDoRegistro()
    {
        const string registro = """{"codigoCentralizador":"1","codigoFornecedor":"2","codigoBaseFornecedor":"3","nomeBaseFornecedor":"4","codigoPoloCentralizador":"5","nomePoloCentralizador":"6","codigoSubPoloCentralizador":"7","nomeSubPoloCentralizador":"8","indicadorSituacaoOrgao":"9","descricaoSituacaoOrgao":"10","codigoSituacaoBloqueioMoedaEstrangeira":"11","descricaoBloqueioMoedaEstrangeira":"12","matriz":"13","agenciaCustodianteBb":"14","tipoBase":"15","descricaoTipoBase":"16"}""";
        var http = FakeHttpMessageHandler.Respondendo(
            HttpStatusCode.OK, """{"data":{"totalRegistros":"1","registros":[""" + registro + "]}}");
        var client = new CentralizadoresClient(CriarHttpClient(http), _auth);

        var response = await client.GetCentralizadorAsync("1", Headers, CancellationToken.None);

        Assert.Equal(registro, JsonSerializer.Serialize(Assert.Single(response.Registros!), JsonHelper.Options));
    }

    private static HttpClient CriarHttpClient(FakeHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.local/base/") };
}
