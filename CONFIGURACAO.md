# Como a configuração chega na Lambda

## O caminho de um valor

```
push em feature/*  ->  PR para develop  ->  merge
                                              |
       workflow 2-develop.yml (reusable do Itaú, lê o .iupipes.yml)
                                              |
   build + testes (app/)  ->  terraform apply em infra/ com inventories/dev/terraform.tfvars
                                              |
   locals.tf junta: default_environment_variables (igual em todo ambiente)
                  + environment_variables da tfvars (muda por ambiente; ganha em caso de conflito)
                                              |
   main.tf entrega esse mapa ao módulo da Lambda  ->  variáveis de ambiente da função
                                              |
   DependencyInjectionResolver lê as env vars  ->  classes *Options do .NET
```

Hom usa `inventories/hom` (branch `release/*`), prod usa `inventories/prod` (PR na `main` + GMUD).

## Cada variável: onde mora e quem lê

| Variável | Onde se define | Classe que lê |
|---|---|---|
| `ECS_BASE_URL` | tfvars do ambiente | `ApiOptions` → `EcsPosicaoClient` |
| `CENTRALIZADOR_BASE_URL` | tfvars do ambiente | `ApiOptions` → `CentralizadoresClient` |
| `AUTH_TOKEN_URL` | tfvars do ambiente | `ApiOptions` → `AuthClient` (token STS) |
| `CORS_ALLOWED_ORIGIN` | tfvars do ambiente | `BffOptions` → header CORS das respostas |
| `Secrets__ConsumoApi` | `locals.tf` (nome fixo) | `SecretsOptions` → `AuthClient` |
| `CHAVE_UNICA_CERTIFICADO` | não definida: vale `Certificados/ca_bundle.crt` (tfvars só se mudar) | `ChaveUnicaOptions` → `ChaveUnicaJwtGenerator` (ver `Certificados/LEIA-ME.md` no projeto Lambda) |
| `SSL_CERT_FILE`, `SSL_CERT_DIR` | `locals.tf` | o próprio .NET (certificados das CAs do banco, da layer) |
| `AWS_LAMBDA_HANDLER_LOG_FORMAT` (`Json`), `AWS_LAMBDA_HANDLER_LOG_LEVEL` | `locals.tf` (o nível vem da variável `application_log_level`) | o runtime .NET da Lambda (formato e nível dos logs; ver "Logs" no Readme da Lambda) |
| `ECS_TIMEOUT_SECONDS`, `CENTRALIZADOR_TIMEOUT_SECONDS`, `API_BASE_PATH`, `BFF_DEFAULT_OPERADOR` | não definidas: valem os padrões (30, 30, `/v1`, `NA`) | `ApiOptions`, `BffOptions`, `PosicaoOptions` |

Faltando URL obrigatória (ou com `PREENCHER`), a Lambda nem sobe: o erro aparece no CloudWatch dizendo qual campo.

## Credenciais

- **AWS (Secrets Manager)**: a Lambda não usa chave de acesso. A role criada pelo `iamsr.tf` (`lambda-ln-app-bff-posicao-ln-l1021`) já dá `secretsmanager:GetSecretValue` e `kms:Decrypt`. As chaves `AWS_ACCESS_KEY_ID`/`SECRET`/`SESSION_TOKEN` que estavam no `Program.cs` do LocalRunner são da sua sessão pessoal: não vão para pipeline nem Terraform, e devem sair do arquivo. Local, use `aws sso login --profile <perfil>` e ponha o perfil em `AWS_PROFILE` no `app/LocalRunner/appsettings.Local.json`.
- **client_id/client_secret do STS**: ficam na secret `lambda-ln-app-bff-posicao/consumo-api`, nunca no código (a foto do `AuthClient` tinha os dois escritos; tire de lá).

## Criar as secrets (uma vez em cada conta: dev, hom, prod)

Console AWS → Secrets Manager → *Store a new secret* → *Other type of secret*:

1. Nome `lambda-ln-app-bff-posicao/consumo-api`, aba *Key/value*: chave `client_id` e chave `client_secret` (os valores do STS do BFF).
2. Só em dev e hom (onde o TAAC roda): nome `taac/lambda-ln-app-bff-posicao`, aba *Key/value*: `client_id` e `client_secret` de um client do STS que possa chamar o API Gateway do BFF. O TAAC pega um token STS com ele, como a lambda de referência.

Se a secret do STS já existir com outro nome, não precisa criar: sobrescreva na tfvars do ambiente, dentro de `environment_variables`: `Secrets__ConsumoApi = "nome-existente"`.

## Certificado da chave-unica (sem secret)

A `chave-unica` é assinada em HS256 com os bytes do `ca_bundle.crt` que vai junto no pacote da Lambda (decisão do
tech lead em 29/10/2026). Coloque o arquivo baixado em `app/src/01-Itau.LN.App.Bff.Posicao.Lambda/Certificados/ca_bundle.crt`
no repositório do Itaú e commite, sem abrir em editor. O ECS valida com o mesmo arquivo: confira o `certificadoSha256`
do log `BFF iniciado` com o hash do arquivo do ECS. Detalhes em `Certificados/LEIA-ME.md`.

## Ordem do primeiro deploy

1. Esta Lambda em dev (merge na `develop`).
2. ApiGateway-Posicao em dev (precisa da Lambda existindo) com `cors_allowed_origin` igual ao `CORS_ALLOWED_ORIGIN` da tfvars.
3. Confirme a URL do API Gateway (domínio/mapping do Itaú ou `invoke_url` com stage dev/hom/prod) e ponha em `TAAC_BASE_URL` no `tests/testspec-dev.yml`. O TAAC roda 3 consultas com STS e 2 cenários de rejeição (token ausente/inválido), com CORS nos erros.

O authorizer de entrada é a Lambda corporativa `valida-token-posicao-custodia-numerario-v1-interno`,
do tipo REQUEST, sem cache, com fonte `method.request.header.authorization`.
Ele é independente do `AuthClient` do BFF, que obtém o STS das chamadas de saída.
O front precisa enviar o token STS; `x-itau-apikey` sozinho não autentica.

O Gateway declara `.read` nas sete consultas e `.write` na reversão, mas a verificação dessas
permissões depende do `/opt/permissions.json` da layer do authorizer. Falta conferir esse arquivo.
O `infra/` local do ApiGateway usa recursos AWS diretos e não deve substituir os módulos
corporativos do banco; ver o README e `ARQUIVOS_MODIFICADOS.md` desse projeto.

## O que ainda está como PREENCHER

| Onde | O quê |
|---|---|
| `infra/inventories/*/terraform.tfvars` | `CORS_ALLOWED_ORIGIN` (URL do front por ambiente); em prod, também as 3 URLs |
| `tests/testspec-dev.yml`, `tests/testspec-hom.yml` | `TAAC_BASE_URL` (invoke_url do ApiGateway) e `quicktest_id` |
