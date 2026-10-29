using System.Diagnostics;
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
    ChaveUnicaService chaveUnicaService,
    IOptions<BffOptions> options)
{
    private const string PosicaoTransportadores = "posicao-transportadores";
    private const string Centralizadores = "centralizadores";

    // Uma linha de log por requisição (ver RegistrarRequisicao), com o motivo de qualquer resposta de erro.
    private const string Resumo =
        "{Metodo} {Path} -> {Status} em {DuracaoMs} ms | correlationId={CorrelationId} centralizador={Centralizador} polo={Polo} transportadora={Transportadora}";

    public async Task<APIGatewayProxyResponse> HandleAsync(
        APIGatewayProxyRequest request, ILambdaContext context, CancellationToken cancellationToken)
    {
        var inicio = Stopwatch.GetTimestamp();
        Exception? erro = null;
        APIGatewayProxyResponse response;
        try
        {
            response = await RotearAsync(request, cancellationToken);
        }
        catch (ApiHttpException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            (response, erro) = (Error(ex.StatusCode, ex.Message), ex);
        }
        catch (ApiHttpException ex)
        {
            (response, erro) = (Error(HttpStatusCode.InternalServerError, ex.Message), ex);
        }
        catch (Exception ex)
        {
            (response, erro) = (Error(HttpStatusCode.InternalServerError, "Erro interno."), ex);
        }

        RegistrarRequisicao(context.Logger, request, response.StatusCode, erro, Stopwatch.GetElapsedTime(inicio));
        return response;
    }

    // Toda resposta de erro sai como ApiHttpException, para o motivo chegar ao log.
    private async Task<APIGatewayProxyResponse> RotearAsync(APIGatewayProxyRequest request, CancellationToken cancellationToken)
    {
        var apiKey = GetHeader(request, HeaderNames.ApiKey);
        var correlationId = GetHeader(request, HeaderNames.CorrelationId);
        if (apiKey is null || correlationId is null)
        {
            throw new ApiHttpException(HttpStatusCode.BadRequest, "Headers obrigatorios ausentes: x-itau-apikey e x-itau-correlationID.");
        }

        // Centralizadores não recebem chave-unica.
        var headers = new ApiRequestHeaders(apiKey, correlationId, string.Empty);

        // Só resolvida depois que a rota casa e a entrada é validada: evita idas ao centralizador em 400/404.
        Task<ApiRequestHeaders> HeadersPosicao() => ComChaveUnicaAsync(request, headers, cancellationToken);

        return (request.HttpMethod?.ToUpperInvariant(), GetRouteSegments(request.Path)) switch
        {
            ("GET", [PosicaoTransportadores]) =>
                await ConsultarCardsAsync(request, HeadersPosicao, cancellationToken),
            ("POST", [PosicaoTransportadores, var indice, "reverter"]) =>
                await ReverterFechamentoAsync(indice, request.Body, HeadersPosicao, cancellationToken),
            ("GET", [PosicaoTransportadores, var indice, "justificativa-divergencias"]) =>
                Ok(await posicaoService.ConsultarDivergenciasAsync(indice, await HeadersPosicao(), cancellationToken)),
            ("GET", [PosicaoTransportadores, var indice, "justificativa-termo-juridico"]) =>
                Ok(await posicaoService.ConsultarTermoJuridicoAsync(indice, await HeadersPosicao(), cancellationToken)),
            ("GET", [Centralizadores, var numero]) =>
                Ok(await centralizadoresClient.GetCentralizadorAsync(numero, headers, cancellationToken)),
            ("GET", [Centralizadores, var numero, "fornecedores", var fornecedorId]) =>
                Ok(await centralizadoresClient.GetFornecedorAsync(numero, fornecedorId, headers, cancellationToken)),
            ("GET", [Centralizadores, var numero, "fornecedores", var fornecedorId, "bases", var baseId]) =>
                Ok(await centralizadoresClient.GetFornecedorInBaseAsync(numero, fornecedorId, baseId, headers, cancellationToken)),
            ("GET", [Centralizadores, var numero, "polos", var poloId]) =>
                Ok(await centralizadoresClient.GetPoloAsync(numero, poloId, headers, cancellationToken)),
            _ => throw new ApiHttpException(HttpStatusCode.NotFound, "Recurso inexistente.")
        };
    }

    // Sucesso => Information; 4xx => Warning com o motivo; 5xx => Error com a exceção (stack trace).
    // Em erro de API de destino, também qual chamada falhou e o status que ela devolveu.
    // Nunca logar x-itau-apikey, Authorization, chave-unica nem bodies.
    private static void RegistrarRequisicao(
        ILambdaLogger logger, APIGatewayProxyRequest request, int status, Exception? erro, TimeSpan duracao)
    {
        object[] campos =
        [
            request.HttpMethod ?? "-",
            request.Path ?? "-",
            status,
            (long)duracao.TotalMilliseconds,
            GetHeader(request, HeaderNames.CorrelationId) ?? "-",
            GetQueryParameter(request, "centralizador") ?? "-",
            GetQueryParameter(request, "polo") ?? "-",
            GetQueryParameter(request, "transportadora") ?? "-"
        ];

        if (erro is null)
        {
            logger.LogInformation(Resumo, campos);
            return;
        }

        const string ResumoComErro = Resumo + " | destino={Destino} statusDestino={StatusDestino} | {Mensagem}";
        object[] destino = erro is ApiHttpException { Destino: { } chamada } apiErro
            ? [chamada, (int)apiErro.StatusCode]
            : ["-", "-"];
        object[] camposComErro = [.. campos, .. destino, erro.Message];

        if (status < 500)
        {
            logger.LogWarning(ResumoComErro, camposComErro);
        }
        else
        {
            logger.LogError(erro, ResumoComErro, camposComErro);
        }
    }

    private async Task<APIGatewayProxyResponse> ConsultarCardsAsync(
        APIGatewayProxyRequest request, Func<Task<ApiRequestHeaders>> headers, CancellationToken cancellationToken)
    {
        var data = GetQueryParameter(request, "data");
        if (string.IsNullOrWhiteSpace(data))
        {
            throw new ApiHttpException(HttpStatusCode.BadRequest, "Parametro obrigatorio ausente: data.");
        }

        if (!DateOnly.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new ApiHttpException(HttpStatusCode.BadRequest, "Parametro data invalido. Utilize o formato yyyy-MM-dd.");
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
            throw new ApiHttpException(HttpStatusCode.BadRequest, "Campo obrigatorio ausente no body: codigoTipoPosicao.");
        }

        await posicaoService.ReverterFechamentoAsync(indiceControlePosicao, payload.CodigoTipoPosicao, await headers(), cancellationToken);
        return Ok(new ReverterFechamentoResponse(true, "Reversao realizada com sucesso", indiceControlePosicao));
    }

    // A chave-unica é sempre o JWT gerado aqui, sem fallback; um header chave-unica do front é ignorado.
    private async Task<ApiRequestHeaders> ComChaveUnicaAsync(
        APIGatewayProxyRequest request, ApiRequestHeaders headers, CancellationToken cancellationToken)
    {
        var centralizador = GetQueryParameter(request, "centralizador");
        var polo = GetQueryParameter(request, "polo");
        var transportadora = GetQueryParameter(request, "transportadora");
        if (string.IsNullOrWhiteSpace(centralizador) || string.IsNullOrWhiteSpace(polo) || string.IsNullOrWhiteSpace(transportadora))
        {
            throw new ApiHttpException(HttpStatusCode.BadRequest, "Parametros obrigatorios ausentes: centralizador, polo e transportadora.");
        }

        return headers with
        {
            ChaveUnica = await chaveUnicaService.ObterAsync(centralizador, polo, transportadora, headers, cancellationToken)
        };
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
