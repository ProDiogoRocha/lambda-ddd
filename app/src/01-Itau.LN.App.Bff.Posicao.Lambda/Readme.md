# Itau.LN.App.Bff.Posicao

BFF (AWS Lambda .NET 10 atrás do API Gateway REST) da posição de transportadoras.
Consome a API ECS (`posicaonumerarionacional/v2`) e a API de centralizadores.

## Projetos

| Projeto | Papel | Referencia |
|---|---|---|
| 01-Lambda | Entrada da Lambda, roteamento, contratos HTTP e composição do DI | Application, Infrastructure |
| 02-Application | Casos de uso, portas (interfaces), DTOs do ECS e mapper | Domain, Shared |
| 03-Domain | Modelo de leitura do card de posição | - |
| 04-Infrastructure | Clients HTTP, OAuth, JWT da chave-unica e Secrets Manager | Application |
| 05-Shared | Constantes de header e JSON | - |

## Rotas

Todas exigem os headers `x-itau-apikey` e `x-itau-correlationID` (400 se ausentes).
O base path `API_BASE_PATH` (padrão `/v1`) é removido antes do roteamento. Pela URL `execute-api` o
stage não chega no path; com domínio customizado chega o base path do mapping, e `API_BASE_PATH`
tem que ser igual a ele.

| Método | Rota | Resposta |
|---|---|---|
| GET | `/posicao-transportadores?data=yyyy-MM-dd` | `{ cards: [...] }` |
| POST | `/posicao-transportadores/{indice}/reverter` (body `{ codigoTipoPosicao }`) | `{ sucesso, mensagem, indiceControlePosicao }` |
| GET | `/posicao-transportadores/{indice}/justificativa-divergencias` | justificativas de divergência |
| GET | `/posicao-transportadores/{indice}/justificativa-termo-juridico` | justificativa do termo jurídico |
| GET | `/centralizadores/{numero}` | `{ totalRegistros, registros: [...] }` |
| GET | `/centralizadores/{numero}/fornecedores/{fornecedorId}` | idem |
| GET | `/centralizadores/{numero}/fornecedores/{fornecedorId}/bases/{baseId}` | idem |
| GET | `/centralizadores/{numero}/polos/{poloId}` | idem |

`chave-unica` só vai para o ECS, nas rotas `/posicao-transportadores`, que exigem `?centralizador=&polo=&transportadora=`
(400 se faltar algum). A Lambda busca o registro do fornecedor em `GET /centralizadores/{centralizador}/fornecedores/{transportadora}`
filtrando pelo polo, e a `chave-unica` é um JWT HS256 (validade de 5 min) com `codigo_transportadora` = `codigoFornecedor` e
`codigo_filial` = `codigoBaseFornecedor` desse registro (texto, como no token que o ECS já aceita), assinado com os bytes do
`ca_bundle.crt` empacotado em `Certificados/` (ver [Certificados/LEIA-ME.md](Certificados/LEIA-ME.md)). O ECS valida com o
mesmo arquivo. Base ausente ou mais de
uma base no polo => 400. Tudo isso só depois que a rota casa e a entrada é validada.
Um header `chave-unica` enviado pelo front é ignorado. Os centralizadores nunca recebem `chave-unica`.

As APIs internas usam certificado da CA privada do banco: a layer de CA bundle (ver `infra/locals.tf`) põe o `ca_bundle.crt`
em `/opt`, e o .NET o carrega pelas env vars `SSL_CERT_FILE`/`SSL_CERT_DIR`, sem código.

Erros: `{ messages: [{ codigo, mensagem }] }`. 400/404 das APIs de destino são repassados; demais viram 500.
Rotas com segmento `.` ou `..` dão 404.

## Logs

Uma linha por requisição, em JSON (`AWS_LAMBDA_HANDLER_LOG_FORMAT=Json`, definido no `infra/locals.tf`). Cada campo do
template vira um campo pesquisável no CloudWatch e no Datadog, junto com o `requestId` da Lambda.

| Nível | Quando | Campos |
|---|---|---|
| Information | 2xx | `Metodo`, `Path`, `Status`, `DuracaoMs`, `CorrelationId`, `Centralizador`, `Polo`, `Transportadora` |
| Warning | 4xx | os mesmos + `Destino`, `StatusDestino` e `Mensagem` (o motivo) |
| Error | 5xx | os mesmos do Warning + stack trace da exceção |

`Destino` é a chamada que falhou (ex.: `GET posicaonumerarionacional.api-sp.dev.aws.cloud.ihf/posicaonumerarionacional/v2/custodias-posicoes-historicos-detalhes-consultar`)
e `StatusDestino` o status que ela devolveu; ficam `-` quando o erro é validação do próprio BFF. Erro do STS traz a URL,
o status e a resposta dele na `Mensagem`. No cold start sai uma linha `BFF iniciado` com as URLs, o nome da secret, o caminho do certificado e o `certificadoSha256` (compare com o do ECS)
carregados. Nunca vão para o log: `x-itau-apikey`, `Authorization`, `chave-unica` e corpos de requisição/resposta.
As chamadas HTTP bem-sucedidas às APIs de destino aparecem como spans no APM do Datadog (`DD_TRACE_ENABLED`).

Nível mínimo: variável `application_log_level` do Terraform (padrão `INFO`).

Consultas no CloudWatch Logs Insights:

```
fields @timestamp, Status, Path, DuracaoMs, Destino, StatusDestino, Mensagem
| filter Status >= 400
| sort @timestamp desc
```

Uma requisição específica: `| filter CorrelationId = "<x-itau-correlationID>"`.

## Variáveis de ambiente

| Variável | Obrigatória | Uso |
|---|---|---|
| `ECS_BASE_URL` | sim | URL base do ECS, com o path: `https://posicaonumerarionacional.<dns do ambiente>/posicaonumerarionacional/v2` |
| `ECS_TIMEOUT_SECONDS` | não (30) | timeout do ECS |
| `CENTRALIZADOR_BASE_URL` | sim | URL base dos centralizadores |
| `CENTRALIZADOR_TIMEOUT_SECONDS` | não (30) | timeout dos centralizadores |
| `AUTH_TOKEN_URL` | sim | URL do token OAuth (client_credentials) |
| `Secrets__ConsumoApi` | sim | secret com `client_id` e `client_secret` |
| `CHAVE_UNICA_CERTIFICADO` | não (`Certificados/ca_bundle.crt`) | arquivo cujos bytes são a chave HS256 do JWT da `chave-unica` |
| `API_BASE_PATH` | não (`/v1`) | base path removido da rota |
| `BFF_DEFAULT_OPERADOR` | não (`NA`) | operador exibido no card |
| `CORS_ALLOWED_ORIGIN` | só com front em outro domínio | origem do front no header `Access-Control-Allow-Origin`; igual ao `cors_allowed_origin` do ApiGateway-Posicao |

Faltando uma obrigatória, a Lambda falha no init.

## Build e testes

```
dotnet build
dotnet test --collect:"XPlat Code Coverage"
```
