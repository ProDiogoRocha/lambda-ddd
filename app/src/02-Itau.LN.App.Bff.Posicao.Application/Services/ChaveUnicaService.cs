using System.Net;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Exceptions;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;

namespace Itau.LN.App.Bff.Posicao.Application.Services;

// A chave-unica é um JWT com a transportadora (codigoFornecedor) e a filial (codigoBaseFornecedor)
// devolvidas pela API de centralizadores: registros do fornecedor no centralizador, filtrados pelo polo.
public sealed class ChaveUnicaService(ICentralizadoresClient centralizadoresClient, IChaveUnicaTokenGenerator tokenGenerator)
{
    public async Task<string> ObterAsync(
        string centralizador, string polo, string transportadora, ApiRequestHeaders headers, CancellationToken cancellationToken)
    {
        var fornecedor = await centralizadoresClient.GetFornecedorAsync(centralizador, transportadora, headers, cancellationToken);
        var filiais = (fornecedor.Registros ?? [])
            .Where(registro => registro.CodigoPoloCentralizador == polo
                && !string.IsNullOrWhiteSpace(registro.CodigoFornecedor)
                && !string.IsNullOrWhiteSpace(registro.CodigoBaseFornecedor))
            .Select(registro => (Transportadora: registro.CodigoFornecedor!, Filial: registro.CodigoBaseFornecedor!))
            .Distinct()
            .ToList();

        // Com mais de uma filial no polo, escolher uma poderia expor a posição de outra filial.
        if (filiais is not [var (codigoTransportadora, codigoFilial)])
        {
            throw new ApiHttpException(HttpStatusCode.BadRequest, filiais.Count == 0
                ? "Base da transportadora nao encontrada no centralizador e polo informados."
                : "Mais de uma base da transportadora no centralizador e polo informados.");
        }

        return await tokenGenerator.GerarAsync(codigoTransportadora, codigoFilial, cancellationToken);
    }
}
