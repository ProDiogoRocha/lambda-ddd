namespace Itau.LN.App.Bff.Posicao.Infrastructure.Auth;

public interface IAuthClient
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}
