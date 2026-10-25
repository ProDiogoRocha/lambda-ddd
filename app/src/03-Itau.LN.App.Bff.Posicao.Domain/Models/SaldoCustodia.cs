namespace Itau.LN.App.Bff.Posicao.Domain.Models;

public sealed record SaldoCustodia(
    string Bloco,
    decimal EntradasValor,
    decimal EntradasDivergencia,
    decimal SaidasValor,
    decimal SaidasDivergencia);
