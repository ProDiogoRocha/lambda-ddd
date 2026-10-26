# O que mudou em relação ao projeto das fotos

Guia para redigitar o projeto no outro computador. Não faz parte do código.

## Ordem sugerida para digitar

1. `05-Shared` e `03-Domain` (não dependem de nada)
2. `02-Application`
3. `04-Infrastructure`
4. `01-Lambda`
5. `app/test`

Compile a cada projeto (`dotnet build`), assim um erro de digitação aparece logo.

## Para onde foi cada arquivo das fotos

| Nas fotos | Agora | Por quê |
|---|---|---|
| Application/Handlers/IPosicaoTransportadorHandler + PosicaoTransportadorHandler | Lambda/Handlers/BffRequestHandler (sem interface) | O handler depende do API Gateway, que é apresentação. O Application não referencia mais o `Amazon.Lambda.*`. Com as rotas de centralizadores, o nome "PosicaoTransportador" deixou de servir. |
| Application/Clients/AuthClient | Infrastructure/Auth/AuthClient + IAuthClient | É implementação de infraestrutura. Virou singleton com o token em cache. |
| Application/Clients/BaseApiClient, CentralizadoresClient, EcsPosicaoClient | Infrastructure/Clients/ | Implementação de infraestrutura. Corrige a referência circular Application <-> Infrastructure. |
| Application/Http/RequestFactory + ResponseParser, Interfaces/Secrets/IRequestFactory + IResponseParser | absorvidos pelo BaseApiClient | Eram interfaces com uma implementação, usadas só pelos clients. |
| Application/Secrets/ConsumoApi/* | AuthClient (records privados) + Infrastructure/Secrets/SecretsManagerExtensions + Options/SecretsOptions | Só o AuthClient usa. |
| Application/Secrets/Rds/* | Infrastructure/Secrets/RdsConnectionProvider + Options/RdsOptions | Infraestrutura. A connection string é montada uma vez por container. |
| Application/Interfaces/Secrets/IConsumoApiSecretProvider, IRdsConnectionProvider | removidas | Uma implementação só. Os testes substituem o `IAmazonSecretsManager`. |
| Application/Interfaces/Clients/*, Repositories/* | Application/Interfaces/ (sem subpastas) | Só três portas. |
| Application/Contracts/Requests/EcsRequestHeaders | Application/Contracts/ApiRequestHeaders (sem Authorization) | Também serve aos centralizadores. O Bearer vem do OAuth. |
| Application/Contracts/Requests/EcsCancelarPosicaoRequest | Application/Contracts/Ecs/ | Contrato do ECS. |
| Application/Contracts/Requests/ReverterFechamentoRequest, Responses/ConsultaCardsResponse, MessagesResponse, ReverterFechamentoResponse | Lambda/Contracts/ | Contratos HTTP do BFF com o front. |
| Application/Contracts/Responses/JustificativaDivergenciasResponse, JustificativaTermoJuridicoResponse | Domain/Models/JustificativaDivergencias, TermoJuridico | Modelos de leitura da posição, como o CardPosicao. O JSON não muda. |
| Lambda/DependencyInjection.cs | Lambda/DependencyInjectionResolver.cs | O nome do arquivo agora bate com a classe. |
| Domain/Entities/Core/Message | Lambda/Contracts/Message | Mensagem de erro HTTP, não domínio. |
| Application/Contracts/Responses/TokenResponse | record privado dentro do AuthClient | Só o AuthClient usa. |
| Application/Contracts/Responses/EcsCancelamentoResponse | removido | O retorno do cancelamento não era usado (e exigia `data`). |
| Application/Contracts/Responses/CentralizadorResponse + Domain/Aggregates/Data + Domain/Entities/Core/Registros | Application/Contracts/Centralizadores/CentralizadorResponse + CentralizadorRegistro | Corrige o `data` duplicado e o PascalCase. |
| Domain/Aggregates/EcsHistoricoDetalhe, Domain/Entities/ECS/* | Application/Contracts/Ecs/ | Contrato de integração, não domínio. Ficaram só os campos usados. |
| Domain/Aggregates/CardPosicao, Entities/Core/Cedula, Moeda, Resumo, SaldoCustodia | Domain/Models/ | Modelo de leitura do card. "Aggregates" não se aplicava. |
| Domain/Entities/Core/Credencial | removido | Nenhum uso. |
| Domain/Exceptions/EcsHttpException | Application/Exceptions/ApiHttpException | Também vale para os centralizadores. |
| Domain/ValueObjects/EcsDataEnvelope, EcsError | records privados no BaseApiClient | Detalhe interno do client. |
| Infrastructure/Options/ApiOptions | Infrastructure/Options/ApiOptions (+ AUTH_TOKEN_URL) | Agora é usada, com validação. |
| Shared/Helpers/HeaderHelper, PathHelper, ResponseHelper | métodos privados do BffRequestHandler | Só o handler usava. O roteamento virou `switch` com list patterns. |
| Shared/Helpers/JsonHelper | Shared/Helpers/JsonHelper (usa `JsonSerializerOptions.Web`) | Usado pelo Lambda e pelo Infrastructure. |
| test/SWB.Lambda.SQSReader.Tests.csproj + FunctionTest.cs | app/test/Itau.LN.App.Bff.Posicao.Tests | O nome era de outro sistema. |

## Correções de bugs das fotos

1. Namespaces `MAP.Itau.LN.App.BFF.Posicao.*` não existiam. Agora tudo está em `Itau.LN.App.Bff.Posicao.*`.
2. `Amazon.Lambda.Core` estava em 2.8.1 num projeto e 3.4.0 no outro. Agora só o Lambda referencia `Amazon.Lambda.*`, na 3.4.0.
3. O csproj tinha `MySql.Data`, mas o código usava `MySqlConnector`. Ficou só o MySqlConnector, e o Dapper foi adicionado.
4. O DI não registrava `IConfiguration`, `IAmazonSecretsManager` nem o provider da secret, e o `AuthClient` não tinha HttpClient nem URL.
5. O token OAuth era sobrescrito pelo Authorization do request (e alterado no `DefaultRequestHeaders`, o que não é thread-safe).
6. Não havia cache: a consulta de cards buscava 1 + N tokens e lia a secret 1 + N vezes.
7. O `Function` resolvia tudo no provider raiz: a conexão MySQL virava única e nunca era fechada, e o DI usava `.GetAwaiter().GetResult()`. Agora há um escopo por invocação e uma conexão aberta por consulta.
8. No Centralizadores, o `data` duplicado fazia o client nunca funcionar.
9. O mapper lia a variável de ambiente lá dentro. Agora o operador vem por options.
10. Se a URL base viesse sem `/` no final, o HttpClient descartava o último segmento.
11. Os segmentos de rota repassados aos centralizadores não eram escapados.
12. Segmentos `..` na rota poderiam levar o BFF a chamar outro endpoint dos centralizadores com o próprio token. Agora dão 404.
13. Com `expires_in` curto (≤ 60s), o token nunca ficava em cache. Agora o cache dura metade da validade.

## Mudanças de comportamento (o front pode notar)

- Erro do ECS com corpo não-JSON: antes dava 500 "Erro interno."; agora a mensagem é "Erro HTTP {status}", com 400/404 repassados e os demais status virando 500.
- Payload sem `data`: a mensagem passou de "Resposta inválida do ECS..." para "Resposta inválida da API de destino...", porque também vale para os centralizadores.
- Números vindos como string no JSON do ECS (ex.: `"10.5"`) agora são aceitos; antes davam erro.
- Timeout da Lambda: falta 1s, a requisição é cancelada e responde 500 "Erro interno." em vez do 502/504 do API Gateway. Atrás do ApiGateway-Posicao, que corta a integração em 29s, com timeout de 30s quem costuma responder é o API Gateway (504 "Tempo de resposta excedido."); no `reverter`, o cancelamento pode concluir no ECS mesmo assim.
- Cancelamento com resposta sem `data` (ex.: 204): antes dava 500; agora dá 200.
- Lista do ECS sem `ocorrencias`: antes provavelmente dava erro; agora devolve `{ "cards": [] }`.
- O POST de cancelamento ao ECS vai em camelCase (antes ia em PascalCase). Confirme o formato que o ECS espera.
- Deixa de existir o 500 "Configuracao ausente: ECS_AUTHORIZATION.", porque o token agora vem do OAuth.
- Configuração obrigatória faltando derruba a Lambda no init (o API Gateway responde 502), em vez de dar erro por request.
- O match de rota diferencia maiúsculas de minúsculas, como as rotas do API Gateway REST.
- Os detalhes dos cards são buscados com até 4 chamadas simultâneas, mantendo a ordem.
- Rotas novas: `GET /centralizadores/...` (4 rotas). Já estão no OpenAPI do ApiGateway-Posicao.
- Com a env `CORS_ALLOWED_ORIGIN`, toda resposta leva `Access-Control-Allow-Origin`: o front chama o API Gateway de outro domínio e, na integração proxy, o header precisa sair da Lambda.
- A chave-unica não vem mais do front. As rotas de posição passaram a exigir `?centralizador=&polo=&transportadora=` (400 "Parametros obrigatorios ausentes: centralizador, polo e transportadora."). Com eles a Lambda busca a base da transportadora na API de centralizadores (fornecedor no centralizador, filtrado pelo polo) e lê a chave na `tbln052` por transportadora (`codtranu`) e base (`filtranu`), com o mesmo `INNER JOIN` na `tbln051` do cadastro de credenciais. Base não encontrada, mais de uma base no polo ou chave não cadastrada dão 400. O header `chave-unica` do front é ignorado e a env `ECS_CHAVE_UNICA` deixou de existir. Os centralizadores não recebem chave-unica nem consultam o banco. A busca só acontece depois que a rota casa e a entrada é validada.
- Atenção: o handler original já lia `transportadora` no GET de cards (sem usar). O front agora precisa enviar também `centralizador` e `polo` em todas as rotas de posição, inclusive no `reverter` e nas justificativas.
- `RdsConfig__ReaderEndpointSecret` foi removido, porque não entrava na connection string.

## Conferir no outro computador

- `aws lambda get-function-configuration`: confira o handler (`Itau.LN.App.Bff.Posicao.Lambda::Itau.LN.App.Bff.Posicao.Lambda.Function::FunctionHandler`), o runtime `dotnet10` e as env vars reais. A lista está no Readme do projeto Lambda.
- As env vars novas são `AUTH_TOKEN_URL` e, se ainda não existirem, `Secrets__ConsumoApi`, `CENTRALIZADOR_*` e `RdsConfig__*`.
- Para o banco, a Lambda precisa de VPC com acesso ao RDS e ao Secrets Manager.
- Confira se o `x-itau-apikey` enviado ao ECS deve ser o do front (como está) ou o client_id do BFF.
- Pasta `Services` (não fotografada): compare a lógica com o PosicaoTransportadorService daqui.
- Chave-unica (ChaveUnicaService): confira no front/OpenAPI do ApiGateway-Posicao os nomes `centralizador`, `polo` e `transportadora`, e se `codigoBaseFornecedor` e `codigoPoloCentralizador` da API de centralizadores vêm no mesmo formato de `filtranu` e do polo enviado (zeros à esquerda quebram a comparação).
- Segurança: a transportadora vem do front, então o front pode consultar outra transportadora. Isso já era assim; vale validar a transportadora do usuário autenticado.
