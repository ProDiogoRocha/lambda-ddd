namespace Itau.LN.App.Bff.Posicao.Domain.Models;

public sealed record Moeda(
    string TextoMoeda,
    decimal Valor,
    decimal Divergencia);
