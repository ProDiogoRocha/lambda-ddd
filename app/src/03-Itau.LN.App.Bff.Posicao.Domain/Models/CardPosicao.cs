namespace Itau.LN.App.Bff.Posicao.Domain.Models;

public sealed record CardPosicao(
    string IndiceControlePosicao,
    string Posicao,
    string Tipo,
    decimal Saldo,
    string Operador,
    string Data,
    string Hora,
    string Situacao,
    decimal SaldoAnterior,
    IReadOnlyCollection<SaldoCustodia> SaldoCustodia,
    IReadOnlyCollection<Cedula> Cedulas,
    IReadOnlyCollection<Moeda> Moedas,
    Resumo Resumo,
    string JustificativaTermoJuridico,
    string JustificativaDivergenciasMovimentacoes,
    string JustificativaDivergenciaPadrao);
