using Dapper;
using Itau.LN.App.Bff.Posicao.Application.Interfaces;
using Itau.LN.App.Bff.Posicao.Infrastructure.Secrets;
using MySqlConnector;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Repositories;

public sealed class CredenciaisRepository(RdsConnectionProvider connectionProvider) : ICredenciaisRepository
{
    private const string ObterChaveUnicaSql = """
        SELECT cod_aces_api_trtd_crcd
        FROM numerario.tbln052_chav_aces_exto_api
        WHERE codtranu = @CodigoTransportadora
        """;

    public async Task<string?> ObterChaveUnicaAsync(string codigoTransportadora, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            await connectionProvider.GetConnectionStringAsync(cancellationToken));

        return await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            ObterChaveUnicaSql,
            new { CodigoTransportadora = codigoTransportadora },
            cancellationToken: cancellationToken));
    }
}
