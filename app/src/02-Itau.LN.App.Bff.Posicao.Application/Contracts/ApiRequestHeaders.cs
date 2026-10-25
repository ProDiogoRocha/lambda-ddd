namespace Itau.LN.App.Bff.Posicao.Application.Contracts;

// Headers repassados às APIs de destino (ECS e Centralizadores).
public sealed record ApiRequestHeaders(string ApiKey, string CorrelationId, string ChaveUnica);
