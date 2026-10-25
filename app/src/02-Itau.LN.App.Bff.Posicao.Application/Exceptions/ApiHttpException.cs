using System.Net;

namespace Itau.LN.App.Bff.Posicao.Application.Exceptions;

// Erro HTTP devolvido por uma API de destino (ECS ou Centralizadores) ou por validação do BFF.
public sealed class ApiHttpException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
