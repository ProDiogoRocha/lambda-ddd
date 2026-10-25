# Itau.LN.App.Bff.Posicao

BFF (AWS Lambda .NET 10 atrás do API Gateway REST) da posição de transportadoras.
Consome a API ECS (`posicaonumerarionacional/v2`) e a API de centralizadores.

## Projetos

| Projeto | Papel | Referencia |
|---|---|---|
| 01-Lambda | Entrada da Lambda, roteamento, contratos HTTP e composição do DI | Application, Infrastructure |
| 02-Application | Casos de uso, portas (interfaces), DTOs do ECS e mapper | Domain, Shared |
| 03-Domain | Modelo de leitura do card de posição | - |
| 04-Infrastructure | Clients HTTP, OAuth, Secrets Manager e repositório MySQL | Application |
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

`chave-unica` enviada às APIs (resolvida só depois que a rota casa e a entrada é validada):
- com `?transportadora=` (qualquer rota): chave cadastrada no banco; sem cadastro => 400, sem fallback;
- sem `transportadora`: header `chave-unica`, senão a env `ECS_CHAVE_UNICA`.

Erros: `{ messages: [{ codigo, mensagem }] }`. 400/404 das APIs de destino são repassados; demais viram 500.
Rotas com segmento `.` ou `..` dão 404.

## Variáveis de ambiente

| Variável | Obrigatória | Uso |
|---|---|---|
| `ECS_BASE_URL` | sim | URL base do ECS |
| `ECS_TIMEOUT_SECONDS` | não (30) | timeout do ECS |
| `CENTRALIZADOR_BASE_URL` | sim | URL base dos centralizadores |
| `CENTRALIZADOR_TIMEOUT_SECONDS` | não (30) | timeout dos centralizadores |
| `AUTH_TOKEN_URL` | sim | URL do token OAuth (client_credentials) |
| `Secrets__ConsumoApi` | sim | secret com `client_id` e `client_secret` |
| `RdsConfig__UserSecret`, `RdsConfig__PasswordSecret`, `RdsConfig__EndpointSecret` | só com `?transportadora=` | secrets do RDS (texto puro ou JSON) |
| `RdsConfig__Database`, `RdsConfig__Port` | só com `?transportadora=` | banco e porta (3306) |
| `API_BASE_PATH` | não (`/v1`) | base path removido da rota |
| `ECS_CHAVE_UNICA` | não | chave-unica padrão |
| `BFF_DEFAULT_OPERADOR` | não (`NA`) | operador exibido no card |
| `CORS_ALLOWED_ORIGIN` | só com front em outro domínio | origem do front no header `Access-Control-Allow-Origin`; igual ao `cors_allowed_origin` do ApiGateway-Posicao |

Faltando uma obrigatória, a Lambda falha no init.

## Build e testes

```
dotnet build
dotnet test --collect:"XPlat Code Coverage"
```
