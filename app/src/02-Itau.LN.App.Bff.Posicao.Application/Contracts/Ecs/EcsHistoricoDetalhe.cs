namespace Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;

// Só os campos que o BFF usa; os demais do ECS são ignorados na desserialização.
public sealed record EcsHistoricoDetalhe(
    string NumeroPosicao,
    string? JustificativaFechamento,
    decimal ValorDivergenciaBloco,
    string? JustificativaDivergenciaBloco,
    decimal ValorSaldoAtualPadrao,
    decimal ValorDivergenciaPadrao,
    string? JustificativaDivergenciaPadrao,
    IReadOnlyList<EcsBlocoOcorrencia>? BlocoOcorrenciaLista,
    IReadOnlyList<EcsCedulaOcorrencia>? CedulasOcorrenciaLista,
    IReadOnlyList<EcsMoedaOcorrencia>? MoedasOcorrenciaLista);
