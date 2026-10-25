namespace Itau.LN.App.Bff.Posicao.Domain.Models;

public sealed record Resumo(
    decimal SaldoAtualMovimentacoes,
    decimal TotalDivergenciasMovimentacoes,
    decimal TotalDivergencia,
    decimal SaldoAtualNaBase);
