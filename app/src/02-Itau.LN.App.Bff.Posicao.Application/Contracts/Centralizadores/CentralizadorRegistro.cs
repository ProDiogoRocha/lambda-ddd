namespace Itau.LN.App.Bff.Posicao.Application.Contracts.Centralizadores;

public sealed record CentralizadorRegistro(
    string? CodigoCentralizador,
    string? CodigoFornecedor,
    string? CodigoBaseFornecedor,
    string? NomeBaseFornecedor,
    string? CodigoPoloCentralizador,
    string? NomePoloCentralizador,
    string? CodigoSubPoloCentralizador,
    string? NomeSubPoloCentralizador,
    string? IndicadorSituacaoOrgao,
    string? DescricaoSituacaoOrgao,
    string? CodigoSituacaoBloqueioMoedaEstrangeira,
    string? DescricaoBloqueioMoedaEstrangeira,
    string? Matriz,
    string? AgenciaCustodianteBb,
    string? TipoBase,
    string? DescricaoTipoBase);
