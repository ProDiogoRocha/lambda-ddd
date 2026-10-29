using System.Web;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Itau.LN.App.Bff.Posicao.Lambda;
using Microsoft.Extensions.Configuration;

// Roda a Function como na Lambda, com as env vars de appsettings.json + appsettings.Local.json (este fora do git).
// As secrets são lidas do Secrets Manager com o perfil de AWS_PROFILE: rode "aws sso login" antes.
// Uso: dotnet run --project app/LocalRunner -- GET "/v1/posicao-transportadores?data=2026-10-28&centralizador=1&polo=2&transportadora=3"
var configuracao = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .Build();

foreach (var (chave, valor) in configuracao.AsEnumerable().Where(item => !string.IsNullOrEmpty(item.Value)))
{
    Environment.SetEnvironmentVariable(chave.Replace(":", "__"), valor);
}

var url = new Uri(new Uri("http://localhost"), args.ElementAtOrDefault(1) ?? "/v1/centralizadores/1");
var query = HttpUtility.ParseQueryString(url.Query);
var request = new APIGatewayProxyRequest
{
    HttpMethod = args.ElementAtOrDefault(0) ?? "GET",
    Path = url.AbsolutePath,
    QueryStringParameters = query.AllKeys.OfType<string>().ToDictionary(chave => chave, chave => query[chave]!),
    Body = args.ElementAtOrDefault(2),
    Headers = new Dictionary<string, string>
    {
        ["x-itau-apikey"] = configuracao["LOCAL_API_KEY"] ?? string.Empty,
        ["x-itau-correlationID"] = Guid.NewGuid().ToString()
    }
};

var response = await new Function().FunctionHandler(request, new TestLambdaContext { RemainingTime = TimeSpan.FromMinutes(1) });
Console.WriteLine($"{response.StatusCode} {response.Body}");
