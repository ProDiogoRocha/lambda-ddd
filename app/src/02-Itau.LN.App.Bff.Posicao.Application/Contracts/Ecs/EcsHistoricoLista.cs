namespace Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;

public sealed record EcsHistoricoLista(IReadOnlyList<EcsHistoricoListaOcorrencia>? Ocorrencias);
