using System.Net;

namespace Itau.LN.App.Bff.Posicao.Application.Exceptions;

// Erro HTTP devolvido por uma API de destino (ECS ou Centralizadores) ou por validação do BFF.
// Destino (método, host e caminho da chamada que falhou) só vai para o log, nunca para o front.
public sealed class ApiHttpException(HttpStatusCode statusCode, string message, string? destino = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? Destino { get; } = destino;
}
