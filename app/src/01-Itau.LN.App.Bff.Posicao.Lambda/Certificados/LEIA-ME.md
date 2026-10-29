# Certificado da chave-unica

O JWT enviado ao ECS no header `chave-unica` é assinado em **HS256** usando como chave os **bytes exatos** do
`ca_bundle.crt` desta pasta. O ECS valida com o **mesmo arquivo**. Decisão do tech lead em 29/10/2026.

## O que colocar aqui

O arquivo **`ca_bundle.crt`**, baixado de `https://caitau.cloud.itau.com.br/ca_bundle.crt` (a partir de uma conta AWS
do banco), sem renomear. Tem que ser **a mesma versão, byte a byte,** que o ECS usa.

- Não abra nem salve o arquivo em editor: trocar quebra de linha, espaço ou encoding muda a chave.
- O `.gitattributes` desta pasta impede o git de converter as quebras de linha do `.crt`.
- No cold start, o log `BFF iniciado` mostra `certificadoSha256`. Compare com o hash do arquivo do ECS
  (`sha256sum ca_bundle.crt` no Linux, `Get-FileHash ca_bundle.crt` no PowerShell): se forem diferentes,
  nenhuma chave-unica será aceita.

Com outro nome ou em outra pasta, informe o caminho em `CHAVE_UNICA_CERTIFICADO` (padrão `Certificados/ca_bundle.crt`,
relativo a `/var/task`). O arquivo entra no pacote da Lambda pelo `.csproj` (extensão `.crt`).

## Atenção

- O arquivo precisa estar commitado no repositório do Itaú para a esteira empacotá-lo.
- O `ca_bundle.crt` é público dentro do banco: quem tiver o arquivo consegue gerar uma chave-unica válida para
  qualquer transportadora.
- Quando o bundle for atualizado pela área de certificados, troque aqui **e** no ECS ao mesmo tempo; enquanto
  forem diferentes, o ECS recusa as chamadas de posição.
