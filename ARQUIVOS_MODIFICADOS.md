# Arquivos modificados (chave-unica via JWT, logs, infra e TAAC)

O que copiar para o repositório do Itaú. "removido" = apagar lá.

## Aplicação

### 01-Lambda

| Path | Situação |
|---|---|
| `app/src/01-Itau.LN.App.Bff.Posicao.Lambda/Itau.LN.App.Bff.Posicao.Lambda.csproj` | alterado (ReadyToRun só com `-r`; sem isso o `dotnet publish` falha com NETSDK1094) |
| `app/src/01-Itau.LN.App.Bff.Posicao.Lambda/Function.cs` | alterado: log `BFF iniciado` no cold start com URLs e nomes das secrets |
| `app/src/01-Itau.LN.App.Bff.Posicao.Lambda/Handlers/BffRequestHandler.cs` | alterado: uma linha de log por requisição (nível pelo status, motivo e API de destino nos erros) |
| `app/src/01-Itau.LN.App.Bff.Posicao.Lambda/Readme.md` | alterado (chave-unica via JWT, seção "Logs", env vars) |
| `app/src/01-Itau.LN.App.Bff.Posicao.Lambda/appsettings.json`, `appsettings.dev.json` | apagar lá (só tinham RDS e o `ApiUrls` sem uso) |

### 02-Application

| Path | Situação |
|---|---|
| `app/src/02-Itau.LN.App.Bff.Posicao.Application/Exceptions/ApiHttpException.cs` | alterado: `Destino` (chamada que falhou, só para log) |
| `app/src/02-Itau.LN.App.Bff.Posicao.Application/Interfaces/IChaveUnicaTokenGenerator.cs` | novo |
| `app/src/02-Itau.LN.App.Bff.Posicao.Application/Interfaces/ICredenciaisRepository.cs` | removido |
| `app/src/02-Itau.LN.App.Bff.Posicao.Application/Options/ApiUrlsOptions.cs` | removido (nas fotos, sem uso: 0 referências) |
| `app/src/02-Itau.LN.App.Bff.Posicao.Application/Services/ChaveUnicaService.cs` | alterado |

### 04-Infrastructure

| Path | Situação |
|---|---|
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Itau.LN.App.Bff.Posicao.Infrastructure.csproj` | alterado (sem Dapper, MySqlConnector e AWSSDK.SSO) |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/DependencyInjection.cs` | alterado |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Auth/AuthClient.cs` | alterado: erro do STS com URL, status e resposta |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Auth/ChaveUnicaJwtGenerator.cs` | novo |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Clients/BaseApiClient.cs` | alterado: preenche o `Destino` nos erros |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Clients/EcsPosicaoClient.cs` | alterado (endpoints relativos à `ECS_BASE_URL`) |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Options/SecretsOptions.cs` | alterado (`ChaveUnicaJwt`) |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Options/RdsOptions.cs` | removido |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Repositories/CredenciaisRepository.cs` | removido |
| `app/src/04-Itau.LN.App.Bff.Posicao.Infrastructure/Secrets/RdsConnectionProvider.cs` | removido |

### Testes unitários

| Path | Situação |
|---|---|
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Itau.LN.App.Bff.Posicao.Tests.csproj` | alterado (`IsPublishable=false`: sem isso as DLLs de teste entram no pacote da Lambda) |
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Auth/AuthClientTests.cs` | alterado (mensagem nova) |
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Auth/ChaveUnicaJwtGeneratorTests.cs` | novo |
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Clients/EcsPosicaoClientTests.cs` | alterado |
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Handlers/BffRequestHandlerTests.cs` | alterado (inclui os testes de log) |
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Handlers/ChaveUnicaPontaAPontaTests.cs` | alterado |
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Services/ChaveUnicaServiceTests.cs` | alterado |
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Repositories/CredenciaisRepositoryTests.cs` | removido |
| `app/test/Itau.LN.App.Bff.Posicao.Tests/Secrets/RdsConnectionProviderTests.cs` | removido |

### LocalRunner

| Path | Situação |
|---|---|
| `app/LocalRunner/LocalRunner.csproj`, `Program.cs`, `appsettings.json` | substitui o de lá. O `Program.cs` de lá tem credenciais AWS no código: não commitar; use `aws sso login` + `AWS_PROFILE` no `appsettings.Local.json` (fora do git) |

## Infra e esteira

| Path | Situação |
|---|---|
| `infra/main.tf` | alterado: runtime `dotnet10`, handler correto, `timeout = 28`, layer de CA bundle, descrição; removidos `aws_lambda_permission.events_permission` e o bloco comentado de SQS |
| `infra/locals.tf` | alterado: `sigla_app`, `LOGGING_LEVEL` (não era lida) trocada por `AWS_LAMBDA_HANDLER_LOG_FORMAT`/`LEVEL`, nomes das secrets, `SSL_CERT_FILE`/`SSL_CERT_DIR`, ARN da layer por ambiente |
| `infra/iamsr/policy-template/invoke-lambda-policies.json` | alterado: libera também `função:*` (alias) |
| `infra/inventories/{dev,hom,prod}/terraform.tfvars` | alterado: `reserved_concurrent_executions` (não declarada, era ignorada e valia 1) virou `concurrent_executions = -1`; novo bloco `environment_variables` com as URLs e o `CORS_ALLOWED_ORIGIN` (preencher os `PREENCHER`) |
| `.iupipes.yml` | alterado: `taac.hom` |
| `tests/testspec-dev.yml`, `tests/testspec-hom.yml` | alterado, no formato do TAAC da lambda de referência (preencher `quicktest_id` e `TAAC_BASE_URL`) |
| `tests/dev/Itau.LN.App.Bff.Posicao.Taac.csproj`, `tests/dev/PosicaoApiTests.cs`, `tests/dev/TokenSts.cs` | novo: 3 consultas integradas com STS e 2 cenários de rejeição pelo authorizer (NUnit + junit.xml) |
| `.gitignore` | alterado (`appsettings.Local.json`, `junit.xml`) |
| `.vscode/launch.json` | alterado (args com a consulta de posição de dev) |
| `CONFIGURACAO.md` | novo: caminho de cada variável, credenciais, secrets a criar e ordem do primeiro deploy |

Iguais às fotos (não precisa copiar): `infra/data.tf`, `iamsr.tf`, `outputs.tf`, `variables.tf`, `lambda-policies.json`, `trust-template/*`, `.vscode/settings.json`.

Não copiar: `MUDANCAS_EM_RELACAO_AS_FOTOS.md` (anotação deste repo) e `app/Itau.LN.App.Bff.Posicao.sln` (lá já está em `app/`).


## API Gateway (projeto irmão)

A equalização das oito operações, authorizer STS, aliases e CORS está em
`../ApiGateway-Posicao`. A lista específica fica em
[ARQUIVOS_MODIFICADOS.md](../ApiGateway-Posicao/ARQUIVOS_MODIFICADOS.md).
A infraestrutura corporativa do Gateway deve ser preservada; gerar o contrato do
ambiente antes de copiá-lo. Falta conferir `permissions.json` e a etapa de importação
OpenAPI do workflow do banco.


Validação na retomada: 106 testes unitários aprovados, 100% das linhas e 98,4% dos
branches. TAAC compilado sem avisos; os testes integrados precisam do ambiente Itaú.
