using Itau.LN.App.Bff.Posicao.Domain.Models;

namespace Itau.LN.App.Bff.Posicao.Lambda.Contracts;

public sealed record ConsultaCardsResponse(IReadOnlyCollection<CardPosicao> Cards);
