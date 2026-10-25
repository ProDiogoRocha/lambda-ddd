namespace Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;

public sealed record EcsBlocoOcorrencia(
    string? DescricaoItemBloco,
    decimal ValorEntradaItemBloco,
    decimal ValorDivergenciaEntradaBloco,
    decimal ValorSaidaItemBloco,
    decimal ValorDivergenciaSaidaBloco);
