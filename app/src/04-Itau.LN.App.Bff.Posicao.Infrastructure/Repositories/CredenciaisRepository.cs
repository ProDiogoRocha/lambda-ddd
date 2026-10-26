using Dapper;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using MySqlConnector;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Repositories;

public sealed class CredenciaisRepository(RdsConnectionProvider connectionProvider) : ICredenciaisRepository
{
    // Mesma leitura do cadastro de credenciais (SWB.Lambda.CadAcessoExtServ): a chave só vale com autorização na 051.
    private const string ObterChaveUnicaSql = """
        SELECT c.cod_aces_api_trtd_crcd
        FROM numerario.tbln052_chav_aces_exto_api c
        INNER JOIN numerario.tbln051_autr_aces_exto_api a
            ON a.cod_aces_api_trtd_crcd = c.cod_aces_api_trtd_crcd
            AND a.num_cnpj_trtd_crcd = c.num_cnpj_trtd_crcd
        WHERE c.codtranu = @CodigoTransportadora
          AND c.filtranu = @CodigoBase
        """;

    public async Task<string?> ObterChaveUnicaAsync(string codigoTransportadora, string codigoBase, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            await connectionProvider.GetConnectionStringAsync(cancellationToken));

        return await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            ObterChaveUnicaSql,
            new { CodigoTransportadora = codigoTransportadora, CodigoBase = codigoBase },
            cancellationToken: cancellationToken));
    }
}
