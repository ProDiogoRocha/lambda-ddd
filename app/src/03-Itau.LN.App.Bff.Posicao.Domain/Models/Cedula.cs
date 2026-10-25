namespace Itau.LN.App.Bff.Posicao.Domain.Models;

public sealed record Cedula(
    string TextoCedula,
    decimal ValorCedulasBoas,
    decimal ValorDilacerado,
    decimal ValorEntintado,
    decimal Divergencia);
