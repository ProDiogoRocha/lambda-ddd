using System.Globalization;
using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Exceptions;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Application.Services;
using Itau.LN.App.Bff.Posicao.Lambda.Contracts;
using Itau.LN.App.Bff.Posicao.Lambda.Options;
using Itau.LN.App.Bff.Posicao.Shared.Constants;
using Itau.LN.App.Bff.Posicao.Shared.Helpers;
using Microsoft.Extensions.Options;

namespace Itau.LN.App.Bff.Posicao.Lambda.Handlers;

public sealed class BffRequestHandler(
    PosicaoTransportadorService posicaoService,
    ICentralizadoresClient centralizadoresClient,
    ICredenciaisRepository credenciaisRepository,
    IOptions<BffOptions> options)
{
    private const string PosicaoTransportadores = "posicao-transportadores";
    private const string Centralizadores = "centralizadores";

    public async Task<APIGatewayProxyResponse> HandleAsync(
        APIGatewayProxyRequest request, ILambdaContext context, CancellationToken cancellationToken)
    {
        try
        {
            var apiKey = GetHeader(request, HeaderNames.ApiKey);
            var correlationId = GetHeader(request, HeaderNames.CorrelationId);
            if (apiKey is null || correlationId is null)
            {
                return Error(HttpStatusCode.BadRequest, "Headers obrigatorios ausentes: x-itau-apikey e x-itau-correlationID.");
            }

            // Só resolvida depois que a rota casa e a entrada é validada: evita ida ao banco em 400/404.
            Task<ApiRequestHeaders> Headers() => CriarHeadersAsync(request, apiKey, correlationId, cancellationToken);

            return (request.HttpMethod?.ToUpperInvariant(), GetRouteSegments(request.Path)) switch
            {
                ("GET", [PosicaoTransportadores]) =>
                    await ConsultarCardsAsync(request, Headers, cancellationToken),
                ("POST", [PosicaoTransportadores, var indice, "reverter"]) =>
                    await ReverterFechamentoAsync(indice, request.Body, Headers, cancellationToken),
                ("GET", [PosicaoTransportadores, var indice, "justificativa-divergencias"]) =>
                    Ok(await posicaoService.ConsultarDivergenciasAsync(indice, await Headers(), cancellationToken)),
                ("GET", [PosicaoTransportadores, var indice, "justificativa-termo-juridico"]) =>
                    Ok(await posicaoService.ConsultarTermoJuridicoAsync(indice, await Headers(), cancellationToken)),
                ("GET", [Centralizadores, var numero]) =>
                    Ok(await centralizadoresClient.GetCentralizadorAsync(numero, await Headers(), cancellationToken)),
                ("GET", [Centralizadores, var numero, "fornecedores", var fornecedorId]) =>
                    Ok(await centralizadoresClient.GetFornecedorAsync(numero, fornecedorId, await Headers(), cancellationToken)),
                ("GET", [Centralizadores, var numero, "fornecedores", var fornecedorId, "bases", var baseId]) =>
                    Ok(await centralizadoresClient.GetFornecedorInBaseAsync(numero, fornecedorId, baseId, await Headers(), cancellationToken)),
                ("GET", [Centralizadores, var numero, "polos", var poloId]) =>
                    Ok(await centralizadoresClient.GetPoloAsync(numero, poloId, await Headers(), cancellationToken)),
                _ => Error(HttpStatusCode.NotFound, "Recurso inexistente.")
            };
        }
        catch (ApiHttpException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            return Error(ex.StatusCode, ex.Message);
        }
        catch (ApiHttpException ex)
        {
            context.Logger.LogError(ex, "Erro HTTP {StatusCode} na API de destino.", (int)ex.StatusCode);
            return Error(HttpStatusCode.InternalServerError, ex.Message);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Erro inesperado no runtime BFF.");
            return Error(HttpStatusCode.InternalServerError, "Erro interno.");
        }
    }

    private async Task<APIGatewayProxyResponse> ConsultarCardsAsync(
        APIGatewayProxyRequest request, Func<Task<ApiRequestHeaders>> headers, CancellationToken cancellationToken)
    {
        var data = GetQueryParameter(request, "data");
        if (string.IsNullOrWhiteSpace(data))
        {
            return Error(HttpStatusCode.BadRequest, "Parametro obrigatorio ausente: data.");
        }

        if (!DateOnly.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return Error(HttpStatusCode.BadRequest, "Parametro data invalido. Utilize o formato yyyy-MM-dd.");
        }

        var cards = await posicaoService.ConsultarCardsAsync(data, await headers(), cancellationToken);
        return Ok(new ConsultaCardsResponse(cards));
    }

    private async Task<APIGatewayProxyResponse> ReverterFechamentoAsync(
        string indiceControlePosicao, string? body, Func<Task<ApiRequestHeaders>> headers, CancellationToken cancellationToken)
    {
        var payload = JsonHelper.DeserializeOrDefault<ReverterFechamentoRequest>(body);
        if (string.IsNullOrWhiteSpace(payload?.CodigoTipoPosicao))
        {
            return Error(HttpStatusCode.BadRequest, "Campo obrigatorio ausente no body: codigoTipoPosicao.");
        }

        await posicaoService.ReverterFechamentoAsync(indiceControlePosicao, payload.CodigoTipoPosicao, await headers(), cancellationToken);
        return Ok(new ReverterFechamentoResponse(true, "Reversao realizada com sucesso", indiceControlePosicao));
    }

    // Com ?transportadora= a chave vem do banco e é obrigatória: sem fallback, para nunca usar a chave de outra transportadora.
    // Sem transportadora: header chave-unica, depois env ECS_CHAVE_UNICA.
    private async Task<ApiRequestHeaders> CriarHeadersAsync(
        APIGatewayProxyRequest request, string apiKey, string correlationId, CancellationToken cancellationToken)
    {
        var transportadora = GetQueryParameter(request, "transportadora");
        if (string.IsNullOrWhiteSpace(transportadora))
        {
            var chaveUnica = GetHeader(request, HeaderNames.ChaveUnica) ?? options.Value.ChaveUnicaPadrao ?? string.Empty;
            return new ApiRequestHeaders(apiKey, correlationId, chaveUnica);
        }

        var chaveDoBanco = await credenciaisRepository.ObterChaveUnicaAsync(transportadora, cancellationToken);

        return string.IsNullOrWhiteSpace(chaveDoBanco)
            ? throw new ApiHttpException(HttpStatusCode.BadRequest, "Chave de acesso nao cadastrada para a transportadora informada.")
            : new ApiRequestHeaders(apiKey, correlationId, chaveDoBanco);
    }

    // Remove o base path (ex.: /v1) e devolve os segmentos da rota.
    private string[] GetRouteSegments(string? path)
    {
        var segments = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            return []; // segmento relativo nunca é repassado às APIs de destino
        }

        var basePath = options.Value.ApiBasePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Take(basePath.Length).SequenceEqual(basePath, StringComparer.OrdinalIgnoreCase)
            ? segments[basePath.Length..]
            : segments;
    }

    private static string? GetHeader(APIGatewayProxyRequest request, string name) =>
        request.Headers?
            .FirstOrDefault(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(header.Value))
            .Value;

    private static string? GetQueryParameter(APIGatewayProxyRequest request, string name) =>
        request.QueryStringParameters?.TryGetValue(name, out var value) == true ? value : null;

    private APIGatewayProxyResponse Ok(object body) => CreateResponse(HttpStatusCode.OK, body);

    private APIGatewayProxyResponse Error(HttpStatusCode statusCode, string mensagem) =>
        CreateResponse(statusCode, new MessagesResponse(
            [new Message(((int)statusCode).ToString(CultureInfo.InvariantCulture), mensagem)]));

    // Integração proxy: o API Gateway não acrescenta CORS na resposta da Lambda, então o header sai daqui.
    private APIGatewayProxyResponse CreateResponse(HttpStatusCode statusCode, object body)
    {
        var headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" };
        var origem = options.Value.CorsAllowedOrigin;
        if (!string.IsNullOrWhiteSpace(origem))
        {
            headers["Access-Control-Allow-Origin"] = origem;
        }

        return new()
        {
            StatusCode = (int)statusCode,
            Headers = headers,
            Body = JsonSerializer.Serialize(body, JsonHelper.Options)
        };
    }
}
