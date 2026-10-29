using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Exceptions;
using Itau.LN.App.Bff.Posicao.Infrastructure.Auth;
using Itau.LN.App.Bff.Posicao.Shared.Constants;
using Itau.LN.App.Bff.Posicao.Shared.Helpers;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Clients;

// Base dos clients: monta headers + Bearer, desembrulha o envelope { "data": ... } e converte erro HTTP em ApiHttpException.
public abstract class BaseApiClient(HttpClient httpClient, IAuthClient authClient)
{
    protected async Task<T> GetAsync<T>(string endpoint, ApiRequestHeaders headers, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await SendAsync(HttpMethod.Get, endpoint, content: null, headers, cancellationToken);

        var envelope = JsonHelper.DeserializeOrDefault<DataEnvelope<T>>(
            await response.Content.ReadAsStringAsync(cancellationToken));

        return envelope?.Data is { } data
            ? data
            : throw new ApiHttpException(
                HttpStatusCode.InternalServerError, "Resposta inválida da API de destino: payload sem campo data.", Destino(response));
    }

    protected async Task PostAsync<TRequest>(string endpoint, TRequest payload, ApiRequestHeaders headers, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post, endpoint, JsonContent.Create(payload, options: JsonHelper.Options), headers, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string endpoint, HttpContent? content, ApiRequestHeaders headers, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, endpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await authClient.GetTokenAsync(cancellationToken));
        request.Headers.Add(HeaderNames.ApiKey, headers.ApiKey);
        request.Headers.Add(HeaderNames.CorrelationId, headers.CorrelationId);

        if (!string.IsNullOrWhiteSpace(headers.ChaveUnica))
        {
            request.Headers.Add(HeaderNames.ChaveUnica, headers.ChaveUnica);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.RequestMessage ??= request; // o HttpClient já trocou a URI relativa pela absoluta: é ela que vai para o log
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var error = JsonHelper.DeserializeOrDefault<ErrorBody>(await response.Content.ReadAsStringAsync(cancellationToken));
            throw new ApiHttpException(
                response.StatusCode,
                error?.Detalhe ?? error?.Mensagem ?? $"Erro HTTP {(int)response.StatusCode}",
                Destino(response));
        }
    }

    // Método, host e caminho, sem a query string.
    private static string? Destino(HttpResponseMessage response) =>
        response.RequestMessage is { RequestUri: { IsAbsoluteUri: true } uri } request
            ? $"{request.Method} {uri.Host}{uri.AbsolutePath}"
            : null;

    private sealed record DataEnvelope<T>(T? Data)
        where T : class;

    private sealed record ErrorBody(string? Mensagem, string? Detalhe);
}
