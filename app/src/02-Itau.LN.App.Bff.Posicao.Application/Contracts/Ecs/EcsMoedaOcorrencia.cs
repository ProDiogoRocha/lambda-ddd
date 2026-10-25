namespace Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;

public sealed record EcsMoedaOcorrencia(
    string? CodigoClassificacaoMoeda,
    decimal ValorFaceMoeda,
    decimal ValorInformadoMoeda,
    decimal ValorDivergenciaItemMoeda);
