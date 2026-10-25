namespace Itau.LN.App.Bff.Posicao.Application.Contracts.Ecs;

public sealed record EcsCedulaOcorrencia(
    string? CodigoClassificacaoCedula,
    decimal ValorFaceCedula,
    decimal ValorInformadoCedula,
    decimal ValorDivergenciaItemCedula);
