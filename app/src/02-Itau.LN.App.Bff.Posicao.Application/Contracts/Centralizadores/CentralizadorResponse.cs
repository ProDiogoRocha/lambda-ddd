namespace Itau.LN.App.Bff.Posicao.Application.Contracts.Centralizadores;

public sealed record CentralizadorResponse(
    string? TotalRegistros,
    IReadOnlyList<CentralizadorRegistro>? Registros);
