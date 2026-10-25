namespace Itau.LN.App.Bff.Posicao.Lambda.Contracts;

public sealed record MessagesResponse(IReadOnlyCollection<Message> Messages);
