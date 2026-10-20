# Testes integrados - Fase 29.2

Status: Concluido. A validacao automatizada passou em 8 cenarios; pedidos/auditoria nas telas Angular e reconexao SSE no navegador tambem foram verificados. A interrupcao de Kafka foi omitida pela justificativa de risco documentada.

## Resultado atual - 20/10/2026

Docker operacional apos reinicializacao pelo usuario. O script original revelou dois defeitos de teste, corrigidos somente nos scripts:

- No Windows PowerShell 5.1, `Invoke-RestMethod` entrega arrays JSON como um unico item do pipeline. `Request` agora enumera explicitamente os itens. A auditoria real existia e os pedidos ja estavam Paid; os filtros do teste produziam falsos timeouts. A mesma causa impedia comparar cada simbolo do snapshot de precos.
- Com Pricing ou PriceFeed confirmadamente parados, Nginx pode exceder os 15s da sonda antes de responder 502/503/504. Esses dois testes aceitam um timeout identificado como evidencia de indisponibilidade, registram esse resultado e continuam exigindo fluxo real e recuperacao apos start. Timeouts de consultas normais continuam sendo falhas.

Reproducao independente em `scripts/Test-Integration.Helpers.ps1`: arrays vazios/unitarios/multiplos, resposta objeto, deteccao de eventos de pagamento, timeout estrito e timeout explicitamente permitido. Passou em Windows PowerShell 5.1 e PowerShell 7 no Windows, sem HTTP real ou Docker.

A verificacao posterior no navegador encontrou um bug real, de severidade MEDIA: apos resposta 502 no handshake, EventSource passava a CLOSED e nao retomava sozinho. `PriceFeedSseService` agora recria a conexao apos 3s nesse caso usando `retry` do RxJS ja existente. O fechamento automatico do Observable libera o EventSource anterior e unsubscribe cancela tambem a tentativa pendente. Dois testes novos cobrem retomada e cancelamento; suite Angular atual: 24/24. Nenhuma dependencia, regra de negocio ou contrato foi adicionado.

Frontend reconstruido com `pnpm build`, `docker compose build frontend` e `docker compose up -d --no-deps --no-build frontend`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Integration.Helpers.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Integration.ps1 -OutputPath .cache\integration-fixed.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Integration.ps1 -IncludeFailures -OutputPath .cache\integration-failures-verified.json
```

As duas execucoes integradas corrigidas retornaram codigo 0. Os JSONs originais do usuario foram preservados. Run final: `phase29-2-cd80e69c5a5f444d8e5ab18b0c7a6963`. Produto: `69ac5393-5ba5-4bec-bd53-93490113d200`, preco 12.34, estoque inicial 10.

| Cenario | Evidencia observada | Status |
| --- | --- | --- |
| Pedido e auditoria | Pedido `77f80458-8260-4918-9ee8-2643d200cebf`: Pending (1) -> Paid (2); dois eventos unicos; SELECTs em order_db/audit_db; Kafka key/envelope conferidos por particao/offset; estoque preservado | PASSOU |
| Entradas invalidas / estoque insuficiente | Quatro respostas HTTP 400; nenhum pedido adicional para o produto de teste | PASSOU |
| SSE e snapshot Kafka/REST | 12 eventos SSE, multiplos simbolos, variacao de precos e timestamps REST avancando | PASSOU |
| Payment indisponivel / retorno | Pedido `dd69028b-fd01-48e5-880c-b372439b9d34` Pending durante parada e Paid apos start; recuperacao observada em aproximadamente 0.52s apos o comando de start; eventos auditados | PASSOU |
| Inventory indisponivel / retorno | HTTP 503 sem pedido persistido; health e produto acessiveis apos start | PASSOU |
| Pricing indisponivel / retorno | HTTP 502; SSE continua com Pricing parado; snapshot recuperado | PASSOU |
| PriceFeed indisponivel / retorno | Timeout registrado com container parado; health recuperado e novos eventos SSE validos apos start | PASSOU |
| Audit indisponivel / retorno | Pedido `d761e155-b937-4aad-a6d6-18c8f4ec8b4c` pago durante parada; OrderCreated e PaymentApproved consultados apos retorno | PASSOU |
| Kafka indisponivel | Omitido pelo risco conhecido de SaveChanges antes de publish | NAO EXECUTADO |
| Telas Angular e reconexao EventSource | Listagem/detalhes do pedido atual como Pago; filtro e payload de auditoria pelo mesmo ID; pagina de precos manteve a sessao aberta durante stop/start e retomou novos eventos sem reload | PASSOU |

Estado final consultado: os 13 servicos persistentes estao running; PostgreSQL, Kafka, Order, Inventory e Pricing healthy; `kafka-init` Exited (0), como esperado. Nenhum servico ficou parado pelos testes. Dados de teste foram preservados. Nenhuma regra de negocio, migration, contrato ou configuracao Nginx foi alterada.

Evidencia do navegador: pedido `77f80458-8260-4918-9ee8-2643d200cebf` exibido como Pago e total R$ 12,34; filtro de auditoria retornou PaymentApproved e OrderCreated; detalhes mostraram EventId `0cbb6b5f-41ee-4ca5-aeab-3a43dfff9a30`, particao 1, offset 4 e key igual ao pedido. Na mesma pagina `/precos`, antes da parada havia CONECTADO; durante a parada, RECONECTANDO com ultimo evento em 10:25:24; apos restauracao, CONECTADO com novos eventos em 10:25:58, em 20/10/2026 (America/Sao_Paulo). Nao houve reload entre esses estados. Esta foi uma verificacao pontual de navegador, sem criar suite E2E visual.

## Bloqueio inicial resolvido - historico

Tentativa em 20/10/2026, Windows / America-Sao_Paulo, branch `feature/part-3`, working tree inicialmente limpa.

O engine Linux do Docker Desktop estava indisponivel antes dos testes. `docker compose ps --all` e `docker version` retornaram erro de conexao ao pipe `dockerDesktopLinuxEngine`. A inicializacao do aplicativo foi tentada, mas o backend encerrou. O diagnostico local em `%LOCALAPPDATA%/Docker/backend.error.json` aponta:

```text
starting services: initializing Inference manager
listening on unix://.../Docker/run/dockerInference
remove .../Docker/run/dockerInference: Nao e possivel o acesso ao arquivo pelo sistema.
```

O log do backend de 20/10/2026 confirmou o encerramento dos processos. Uma consulta HTTP fora do sandbox confirmou conexao recusada em `localhost:4200`.

Na primeira tentativa, nenhum container foi parado pelo script e nenhum produto/pedido foi criado. O estado individual dos containers nao podia ser consultado com o engine indisponivel. Nao foram alteradas configuracoes do Docker Desktop nem removidos sockets para contornar o problema. O bloqueio foi resolvido apos o usuario reiniciar o Docker; a matriz atual acima substitui o resultado inicial.

## Contratos inspecionados

| Componente | Configuracao real |
| --- | --- |
| Frontend / Nginx | `localhost:4200`; proxies `/api/order`, `/api/inventory`, `/api/pricing`, `/api/pricefeed`, `/api/audit` |
| Order.Service | `5074`; `GET/POST /orders`, `GET /orders/{id}`, banco `order_db` |
| Inventory.Service | HTTP `5276`, gRPC `7242`; `/products`, `/products/{id}`, `/products/{id}/availability`; banco `inventory_db` |
| gRPC Inventory | `CheckAvailability` e `ReserveStock` existem; pedidos usam somente `CheckAvailability` |
| Payment.Service | Sem HTTP; consumer group `payment-service`; consome `order-created`, publica `payment-approved` ou `payment-rejected` |
| Audit.Worker | `5288`; `/health`, `/audit-events`; filtros `eventType`, `messageKey`/`orderId`, `limit`, `offset`; banco `audit_db` |
| PriceFeed.Service | HTTP `5080`, gRPC `19090`; `/prices/stream` SSE, evento `price-update`; publica `price-updated` |
| Pricing.Service | `5090`; `/prices`, `/prices/{symbol}`; consumer group `pricing-service`; estado em memoria; gRPC lab desabilitado no Compose |
| Kafka | Host `9092`, rede interna `kafka:29092`; volumes nomeados no Compose |

Os nomes de containers devem ser obtidos com `docker compose ps --all`. O script usa nomes de servicos do Compose, sem presumir IDs de containers.

## Execucao reproduzivel

Pre-requisitos: Docker Desktop com engine Linux operacional, Docker Compose, Windows PowerShell 5.1 ou PowerShell 7 e servicos existentes do laboratorio ativos. Executar na raiz do repositorio:

```powershell
docker compose config --quiet
docker compose ps --all
docker compose up -d --build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Integration.ps1 -OutputPath .cache\integration.json
```

Falhas controladas opcionais, sequenciais (interrompem brevemente os servicos indicados):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Integration.ps1 -IncludeFailures -OutputPath .cache\integration-failures.json
```

No PowerShell 7 tambem e possivel invocar o script diretamente. `-BaseUrl` permite alterar a URL do frontend e `-TimeoutSeconds` limita esperas de estado (padrao 90s). Requisicoes HTTP possuem limite de 15s; comandos Compose, 60s; leituras SSE tambem sao limitadas. O script retorna codigo 1 se houver falha, incluindo pre-condicoes ou restauracao.

Resultados JSON ficam no caminho escolhido e nao sao versionados quando armazenados em `.cache/`. O script gera produto com prefixo `phase29-2-` e pedidos com IDs novos. Preserva esses registros para investigacao; nao faz limpeza de dados. Seus SELECTs no PostgreSQL sao somente leitura. A leitura Kafka usa a particao/offset do evento atual e nao altera offsets dos consumer groups da aplicacao.

O script verifica servicos ativos antes de criar dados. `-IncludeFailures` usa `stop/start` de um servico por vez e `finally` para restauracao. Se a restauracao falhar, os proximos cenarios sao interrompidos e o erro e registrado. O processo pode ser interrompido externamente antes do finally; nesse caso, use o estado inicial registrado para restaurar o servico afetado.

## Matriz da tentativa inicial - historico

| Cenario | Esperado | Observado / evidencia | Status |
| --- | --- | --- | --- |
| Pre-condicoes Docker | Engine e servicos acessiveis | Pipe Linux ausente; backend encerra ao inicializar inference | FALHOU |
| Pedido via proxy Angular / gRPC / pagamento | Persistencia e status terminal | Script interrompido antes da criacao de dados | NAO EXECUTADO |
| Entrada invalida / estoque insuficiente | HTTP 400; nenhum pedido salvo | Engine indisponivel | NAO EXECUTADO |
| Auditoria / Kafka / PostgreSQL | Envelope correlacionado, EventId unico, filtros corretos | Engine indisponivel | NAO EXECUTADO |
| Precos REST e SSE | Multiplos simbolos, payload valido e timestamps REST avancando | Engine indisponivel | NAO EXECUTADO |
| Payment parado / recuperacao | Pending durante parada; terminal apos start; tempo registrado | Engine indisponivel | NAO EXECUTADO |
| Inventory parado / recuperacao | HTTP 503; nenhum pedido salvo; consulta recupera | Engine indisponivel | NAO EXECUTADO |
| Pricing parado / recuperacao | REST indisponivel; SSE continua; snapshot recupera | Engine indisponivel | NAO EXECUTADO |
| PriceFeed parado / recuperacao | Stream indisponivel; novos eventos apos start | Engine indisponivel | NAO EXECUTADO |
| EventSource em navegador | Conexao existente interrompe e reconecta apos recuperacao | Requer navegador com ambiente operacional | NAO EXECUTADO |
| Audit parado / recuperacao | Pagamento prossegue; eventos aparecem apos start | Engine indisponivel | NAO EXECUTADO |
| Consultas pelas telas Angular | Pedido e eventos atuais visiveis nas telas | Nenhum teste de navegador executado | NAO EXECUTADO |
| Kafka indisponivel | Avaliar consistencia e recuperacao | Teste omitido por risco conhecido de save antes de publish | NAO EXECUTADO |

Na tentativa inicial, o script retornou 1 nas pre-condicoes. A sintaxe e o helper Compose foram verificados separadamente naquela ocasiao. A execucao real posterior e os resultados corrigidos constam na matriz atual.

## Regressoes executadas

| Comando | Resultado |
| --- | --- |
| `pnpm test:ci` no frontend | PASSOU: 24/24 apos correcao SSE |
| `pnpm build` no frontend | PASSOU |
| `dotnet build .\MicroservicesLab.sln` | PASSOU: 0 warnings, 0 errors |
| `dotnet test .\MicroservicesLab.sln --no-build` | PASSOU: 32/32 |
| `go test ./...` em Payment.Service | PASSOU |
| `go test ./...` em PriceFeed.Service | PASSOU |
| `docker compose config --quiet` | PASSOU; nao exige engine |

## Problemas e classificacao

| Item | Tipo | Severidade | Diagnostico / encaminhamento |
| --- | --- | --- | --- |
| Docker Desktop nao iniciava engine | Bloqueio operacional resolvido | CRITICO na tentativa inicial | Resolvido apos reinicializacao pelo usuario; engine e fluxos agora acessiveis |
| Arrays REST no Windows PowerShell 5.1 | Bug confirmado no script, corrigido | MEDIO | Enumeracao explicita no helper Request; regressao independente e execucao integrada aprovadas |
| Timeout do proxy durante parada esperada | Bug de expectativa no teste, corrigido | MEDIO | Apenas sondas de Pricing/PriceFeed parados permitem timeout registrado; recuperacao continua obrigatoria |
| EventSource nao retomava apos handshake 502 | Bug confirmado no frontend, corrigido | MEDIO | Recriacao apos CLOSED com intervalo de 3s; testes de retomada/cancelamento e verificacao real no navegador aprovados |
| SaveChanges antes do publish | Limitacao conhecida, inspecao de codigo | ALTO | `src/services/Order.Service/Services/OrdersAppService.cs`; falha de publish pode deixar pedido salvo sem evento; discutir consistencia na Fase 31 |
| Pedido nao reserva estoque | Comportamento atual esperado | MEDIO | Mesmo arquivo e contrato Inventory; reserva, concorrencia, idempotencia e compensacao ficam na Fase 31 |
| Recuperacao dos consumidores | Validada nos cenarios controlados executados | Nao atribuida | Payment e Audit retomaram os pedidos atuais apos start; isso nao comprova exactly-once ou todas as janelas de falha |

## Retomada e recuperacao

1. Confirmar `docker version` com secao Server e `docker compose ps --all` antes de novas rodadas.
2. Subir os servicos necessarios preservando volumes; verificar healths de Order, Inventory, Audit, Pricing e PriceFeed. Payment nao possui health HTTP.
3. Executar os scripts acima e revisar cada resultado, sem ignorar falhas de restore.
4. Se um teste for interrompido externamente, usar `docker compose start <servico>` para o servico afetado e confirmar health/fluxo antes de outro cenario.
5. Validar no navegador `/pedidos`, `/auditoria` e `/precos`; usar IDs da execucao atual. Manter a pagina de precos aberta durante uma parada/restauracao controlada do PriceFeed e confirmar reconexao real do EventSource.
6. Registrar novas evidencias se repetir os cenarios. A Fase 29.2 foi concluida com os resultados acima; a revisao posterior da Fase 29.3 esta em [TECHNICAL_REVIEW.md](TECHNICAL_REVIEW.md).

## Revisao posterior - Fase 29.3

A matriz de oito cenarios e evidencia historica da Fase 29.2, nao uma nova execucao. Nesta revisao foram executadas regressoes e smoke tests somente leitura, sem parar servicos, criar produtos/pedidos ou interromper Kafka.
Os testes Angular de health agora cobrem timeout/cancelamento em 5s e consulta posterior; a suite passou com 26/26. O Compose passa a interromper kafka-init em erro e aguardar o container Audit antes do frontend.
Essa ordenacao nao equivale a readiness nem comprova recuperacao apos recriacao de upstreams com IP diferente. Nao foi repetido cold start nem falha controlada.
Detalhes dos comandos, resultados e limites de cobertura estao no relatorio tecnico.

Nao executar `down -v`, remocao de volumes, prune, DROP, TRUNCATE ou DELETE em massa. SSE e REST seguem caminhos diferentes; nao exigir igualdade instantanea dos precos.
