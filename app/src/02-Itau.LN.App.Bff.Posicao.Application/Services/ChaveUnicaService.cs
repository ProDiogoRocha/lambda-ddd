using System.Net;
using Itau.LN.App.Bff.Posicao.Application.Contracts;
using Itau.LN.App.Bff.Posicao.Application.Exceptions;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;

namespace Itau.LN.App.Bff.Posicao.Application.Services;

// A chave-unica fica no RDS por transportadora (codtranu) e base (filtranu).
// A base vem da API de centralizadores: registros do fornecedor no centralizador, filtrados pelo polo.
public sealed class ChaveUnicaService(ICentralizadoresClient centralizadoresClient, ICredenciaisRepository credenciaisRepository)
{
    public async Task<string> ObterAsync(
        string centralizador, string polo, string transportadora, ApiRequestHeaders headers, CancellationToken cancellationToken)
    {
        var fornecedor = await centralizadoresClient.GetFornecedorAsync(centralizador, transportadora, headers, cancellationToken);
        var bases = (fornecedor.Registros ?? [])
            .Where(registro => registro.CodigoPoloCentralizador == polo && !string.IsNullOrWhiteSpace(registro.CodigoBaseFornecedor))
            .Select(registro => registro.CodigoBaseFornecedor!)
            .Distinct()
            .ToList();

        // Com mais de uma base no polo, escolher uma poderia usar a chave de outra filial.
        if (bases is not [var codigoBase])
        {
            throw new ApiHttpException(HttpStatusCode.BadRequest, bases.Count == 0
                ? "Base da transportadora nao encontrada no centralizador e polo informados."
                : "Mais de uma base da transportadora no centralizador e polo informados.");
        }

        var chave = await credenciaisRepository.ObterChaveUnicaAsync(transportadora, codigoBase, cancellationToken);

        return string.IsNullOrWhiteSpace(chave)
            ? throw new ApiHttpException(HttpStatusCode.BadRequest, "Chave de acesso nao cadastrada para a transportadora informada.")
            : chave;
    }
}
