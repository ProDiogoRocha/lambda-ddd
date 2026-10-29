using Microsoft.Extensions.Configuration;

namespace Itau.LN.App.Bff.Posicao.Infrastructure.Options;

// Arquivo cujo conteúdo é a chave HS256 da chave-unica, empacotado com a Lambda (pasta Certificados do projeto Lambda).
public sealed class ChaveUnicaOptions
{
    // Relativo à pasta da aplicação (/var/task na Lambda); aceita caminho absoluto.
    [ConfigurationKeyName("CHAVE_UNICA_CERTIFICADO")]
    public string Certificado { get; init; } = "Certificados/ca_bundle.crt";
}
