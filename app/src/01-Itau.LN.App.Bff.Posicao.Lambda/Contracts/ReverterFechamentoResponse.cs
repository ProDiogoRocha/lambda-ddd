namespace Itau.LN.App.Bff.Posicao.Lambda.Contracts;

public sealed record ReverterFechamentoResponse(bool Sucesso, string Mensagem, string IndiceControlePosicao);
