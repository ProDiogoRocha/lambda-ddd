namespace Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;

public sealed record EcsHistoricoListaOcorrencia(
    string IndiceControlePosicao,
    string DescricaoPosicao,
    string DescricaoSituacaoPosicao,
    decimal ValorSaldoAnteriorComposicao,
    decimal ValorSaldoAtualComposicao,
    string DataCadastro,
    string HoraCadastro);
