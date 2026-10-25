namespace Itau.LN.App.Bff.Posicao.Domain.Models;

public sealed record JustificativaDivergencias(
    string IndiceControlePosicao,
    decimal TotalDivergenciasMovimentacoes,
    string JustificativaDivergenciasMovimentacoes,
    decimal TotalDivergenciaPadrao,
    string JustificativaDivergenciaPadrao);
