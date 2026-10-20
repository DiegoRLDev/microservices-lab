# Revisao tecnica - Fase 29.3

Data: 20/10/2026. Branch: `feature/part-3`. Working tree inicialmente limpa.
Escopo: seis servicos, Angular, contratos, Docker Compose, testes e consolidacao documental. Nenhuma funcionalidade nova, alteracao de negocio, migration, dependencia ou mudanca no processamento Kafka.

## Resultado e criterio

Fase 29.3 concluida. Nenhum bloqueador CRITICO novo foi observado nos fluxos inspecionados e no smoke. Os riscos ALTOS abaixo sao limites conhecidos ou janelas potenciais de falha confirmadas pelo desenho do codigo, nao incidentes de perda de dados reproduzidos nesta revisao. Nao foram corrigidos automaticamente por exigirem decisoes de consistencia.

CRITICO impede o fluxo principal; ALTO permite inconsistencia/falha/perda relevante; MEDIO afeta confiabilidade/manutencao; BAIXO e nao bloqueante. Pendente nao significa que uma fase futura ja foi iniciada.

## Achados

| ID / severidade | Tipo e evidencia | Impacto | Recomendacao / status |
| --- | --- | --- | --- |
| R1 - ALTO | Limitacao conhecida, janela potencial: [OrdersAppService](../src/services/Order.Service/Services/OrdersAppService.cs), `CreateAsync`, executa SaveChanges antes de PublishAsync | Pedido pode permanecer Pending sem OrderCreated; repetir POST pode duplicar a intencao | PENDENTE Fase 31: definir consistencia e recuperacao com idempotencia. Nao introduzir Outbox/Saga automaticamente |
| R2 - ALTO | Risco conhecido: [Payment consumer](../src/services/Payment.Service/internal/kafka/order_created_consumer.go), `Run`, publica resultado antes de CommitMessages, sem registro duravel de eventos processados | Falha entre publish e commit permite novo pagamento simulado/evento na reentrega. Idempotencia do producer Order nao cobre essa janela | PENDENTE Fase 31: definir identidade de operacao e deduplicacao. Audit deduplica EventId, nao eventos semanticamente iguais com IDs novos |
| R3 - ALTO | Limite conhecido: [InventoryAppService](../src/services/Inventory.Service/Services/InventoryAppService.cs), `ReserveStockAsync`, le/altera/salva sem token de concorrencia ou UPDATE condicional; Order usa apenas CheckAvailability | Reserva existente nao garante correcoes concorrentes; compras podem consultar o mesmo saldo. A ausencia de reserva em Order e comportamento esperado, nao regressao nova | PENDENTE Fase 31: reserva atomica, idempotencia, concorrencia, compensacao e falhas distribuidas. Nao ativar reserva atual no pedido |
| R4 - MEDIO | Risco de recuperacao: [PaymentEventsConsumer](../src/services/Order.Service/Infrastructure/Kafka/PaymentEventsConsumer.cs), [KafkaAuditConsumer](../src/workers/Audit.Worker/Infrastructure/Kafka/KafkaAuditConsumer.cs) e Payment consumer deixam erros de processamento escaparem do loop; Compose nao define restart automatico | Payload invalido, erro de banco ou evento de pagamento conflitante pode encerrar host/consumer. Stop/start validado nao cobre essas falhas | PENDENTE: avaliar retry/poison messages/operacao antes de alterar consumidores. Nao modificar processamento Kafka nesta revisao |
| R5 - MEDIO | Limite confirmado por codigo: [LatestPriceStore](../src/services/Pricing.Service/Services/LatestPriceStore.cs) e [KafkaPriceUpdatedConsumer](../src/services/Pricing.Service/Infrastructure/Kafka/KafkaPriceUpdatedConsumer.cs): memoria, offsets persistidos no grupo e substituicao pelo ultimo recebido | Reinicio pode deixar snapshot vazio/sem simbolos ate novos eventos; nao reconstroi automaticamente valores ja commitados. Evento antigo recebido depois pode substituir preco mais recente por timestamp | PENDENTE: documentado; avaliar recuperacao/semantica de ordenacao antes de uso alem do laboratorio. Sem persistencia adicionada |
| R6 - MEDIO | Bug confirmado por inspecao: [LabHealthService](../src/frontend/MicroservicesLab.App/src/app/core/api/lab-health.service.ts) nao tinha timeout; [ObservabilityPage](../src/frontend/MicroservicesLab.App/src/app/pages/observability/observability-page.component.ts) usa forkJoin e trava refresh enquanto isRefreshing | Health que nao termina impede nova rodada de health | CORRIGIDO: timeout de 5s nos dois caminhos, cancelamento HTTP e erro tratado pela tela. Dois casos de regressao verificam cancelamento e consulta posterior |
| R7 - MEDIO | Bug de configuracao: [Compose](../docker-compose.yml), command de kafka-init, executava varios comandos sem fail-fast | Falha em criar um topico podia ser mascarada pelo exit 0 de comando posterior, liberando dependentes | CORRIGIDO: set -e, mantendo --if-not-exists. Shell extraido da configuracao foi testado com comandos substituidos por false, sem tocar topicos |
| R8 - MEDIO | Risco potencial de cold start: [nginx.conf](../src/frontend/MicroservicesLab.App/nginx.conf) referencia audit-worker, mas frontend nao dependia dele no [Compose](../docker-compose.yml) | Nginx pode iniciar antes de o upstream possuir DNS disponivel | CORRIGIDO preventivamente: dependencia service_started de Audit. Validada na configuracao; cold start nao repetido. Nao implica readiness HTTP |
| R9 - MEDIO | Riscos localizados por inspecao: [OrdersPage](../src/frontend/MicroservicesLab.App/src/app/pages/orders/orders-page.component.ts), selectOrder/startPolling; [DashboardPage](../src/frontend/MicroservicesLab.App/src/app/pages/dashboard/dashboard-page.component.ts), forkJoin dos indicadores | Polling usa exhaustMap mas GET sem timeout pode bloquear tentativas; Pending respondendo 200 nao tem prazo total. Consultas de detalhes A/B simultaneas podem exibir resposta antiga por ultimo. Timeout de health nao limita as outras APIs do dashboard | PENDENTE de manutencao: definir prazos e cancelar consulta de detalhes anterior com testes especificos. Nao atribuir a regras de estoque nem alegar que foi reproduzido no navegador |
| R10 - MEDIO | Risco potencial de operacao: Nginx tem upstreams estaticos; depende de DNS resolvido na inicializacao | Recriar backend com IP diferente nao equivale ao stop/start usado na Fase 29.2; proxy pode precisar de reload/reinicio | PENDENTE: avaliar resolucao DNS dinamica em mudanca dedicada. Nao alegar validacao de recriacao; consultar/recarregar frontend deliberadamente se necessario |
| R11 - BAIXO | Inconsistencias confirmadas: README extenso/duplicado, guia local dizia Audit sem HTTP, diagrama antigo ligava Kafka a SSE; PROGRESS misturava evidencias de 23/26.2/27/28.1 | Comandos e interpretacoes incorretas, fases futuras confundidas com implementadas | CORRIGIDO: README operacional com links, guia local atualizado, diagramas dos caminhos independentes e realocacao das evidencias preservadas |
| R12 - BAIXO | Lacuna de cobertura: specs de componentes instanciam classes com mocks/any, sem renderizar templates; testes de servicos .NET usam EF InMemory; testes Pricing HTTP desabilitam hosted services | Testes unitarios nao comprovam DOM, constraints/transacoes PostgreSQL ou entrega Kafka real | PENDENTE: cobertura proporcional em evolucoes futuras. Build verifica templates de producao; matriz 29.2 e evidencia integrada separada, nao suite E2E visual automatizada |

Nao houve reproducao invasiva dos riscos R1-R5, R8-R10. Os dois bugs corrigidos de health/shell possuem verificacoes direcionadas; o ajuste de ordenacao Compose foi validado estaticamente. Esses limites sao explicitos para nao confundir risco potencial com incidente observado.

## Contratos e responsabilidades conferidos

| Servico | Responsabilidade / contrato real | Persistencia / comunicacao |
| --- | --- | --- |
| Order .NET | GET/POST /orders, GET /orders/{id}; resposta id, productId, quantity, unitPrice, totalPrice, status, createdAt | order_db; gRPC CheckAvailability; publica OrderCreated e consome os dois eventos de pagamento |
| Inventory .NET | GET/POST /products, GET /products/{id}, POST availability e reserve | inventory_db; gRPC CheckAvailability e ReserveStock; reserva nao consumida por Order/Angular |
| Payment Go | Consome order-created; simula decisao pelo valor total; publica payment-approved/rejected | Sem HTTP/banco; group payment-service. Valores JSON float64 no Go sao limite didatico atual, sem mudanca de contrato |
| Audit .NET | GET /audit-events: eventType, messageKey/orderId, limit 1..200, offset >= 0; GET /health | audit_db; mesmo worker e consumidor group audit-worker. Payload jsonb e indice unico EventId; consulta AsNoTracking |
| PriceFeed Go | Fonte simulada desacoplada, console, SSE /prices/stream, gRPC StreamPrices e Kafka opcional | Sem banco; symbol como key de price-updated, price em centavos int64; cancelamento por context |
| Pricing .NET | Consome price-updated, GET /prices e /prices/{symbol} | Estado em memoria; group pricing-service. gRPC lab opcional imprime logs, nao atualiza REST; desabilitado no Compose |

Contratos Protobuf em [inventory.proto](../src/protos/inventory.proto) e [pricefeed.proto](../src/protos/pricefeed.proto). Inventory transmite unit_price como string decimal invariant; REST de produtos/pedidos usa unidades monetarias. PriceFeed/SSE/Pricing usam centavos; Angular divide por 100 somente nesse fluxo.

Eventos de negocio compartilham envelope eventId/eventType/occurredAt/correlationId/data, com OrderId como key. PriceUpdated e JSON simples symbol/price/occurredAt, sem envelope. Audit consome somente os tres topicos de negocio, nao price-updated. Nao foi encontrada incompatibilidade que exigisse alterar contratos HTTP/gRPC/Kafka nesta revisao.

Angular mantem HttpClient nos servicos, endpoints centralizados e rotas standalone. takeUntilDestroyed encerra subscriptions; tela de precos limpa timers; Observable SSE fecha EventSource no teardown e cancela retry pendente. Observabilidade usa intervalo de 15s sem sobrepor rodadas e o libera na destruicao. Pedidos usam intervalo de 2,5s, exhaustMap e limite de cinco falhas consecutivas (nao um limite total de duracao).

Repeticao de metadados entre alvos de health do dashboard e observabilidade foi mantida: conjuntos diferentes e custo pequeno nao justificam refatoracao. Nao foram alterados estilos, layout ou responsividade.

## Infraestrutura e limites operacionais

- Compose usa rede default do projeto, PostgreSQL e Kafka com volumes nomeados. Nenhum volume/banco foi removido ou recriado.
- Bancos conferidos por consulta somente leitura: audit_db, inventory_db e order_db. Topicos de negocio/precos conferidos com kafka-topics --list.
- Nginx e proxy de desenvolvimento removem os mesmos cinco prefixos /api; SSE tem buffering desabilitado; rotas SPA tem fallback index.html. URLs externas das ferramentas sao localhost e precisam ser adaptadas para acesso por outra maquina.
- Order/Inventory/Audit instrumentados via helper OTLP .NET; Payment instrumentado em Go. Collector exporta traces para Jaeger e metricas para :9464; Prometheus faz scrape; Grafana consulta Prometheus. Nao ha pipeline de logs centralizados nem instrumentacao OTLP de PriceFeed/Pricing.
- Payment sem health HTTP e limitacao conhecida. Healths existentes/TCP nao comprovam consumo Kafka, freshness do snapshot ou ausencia de lag.
- Single broker, replication factor 1, credenciais locais, portas publicadas sem autenticacao/TLS: laboratorio, nao configuracao de producao. Persistencia de PostgreSQL/Kafka nao significa retencao garantida dos dados das ferramentas de observabilidade.
- Revisao idiomatica futura do Payment permanece desejada como avaliacao, sem supor que sua organizacao esteja errada; nenhum arquivo dele foi modificado.

## Testes e reprodutibilidade

Executados nesta Fase 29.3, em 20/10/2026:

| Comando / verificacao | Resultado |
| --- | --- |
| pnpm test:ci, no frontend | PASSOU, 26/26 (24 anteriores + dois casos do timeout health) |
| pnpm build, no frontend | PASSOU |
| dotnet build .\MicroservicesLab.sln | PASSOU, 0 warnings/erros |
| dotnet test .\MicroservicesLab.sln --no-build | PASSOU, 32/32: Order 9, Inventory 5, Audit 7, Pricing 11 |
| go test ./..., Payment.Service | PASSOU; resultados em cache quando indicado pelo Go |
| go test ./..., PriceFeed.Service | PASSOU; resultados em cache quando indicado pelo Go |
| docker compose config --quiet | PASSOU |
| Test-Integration.Helpers.ps1 em powershell.exe e pwsh | PASSOU nos dois; nao executa o runner integrado |
| Config JSON Compose: frontend depende de Audit | PASSOU, service_started |
| Script shell de init com chamadas Kafka substituidas por false | PASSOU: exit 1 antes de marcador final; executado em processo shell efemero dentro do container existente, sem alterar dados |
| git diff --check | PASSOU |

Smoke nao destrutivo, usando a imagem frontend ja ativa, sem deploy do codigo novo:
- GET das seis rotas Angular: HTTP 200 (verifica fallback/entrega da SPA, nao renderizacao ou navegacao de navegador).
- GET dos cinco /api/*/health e listas de pedidos, produtos, precos e auditoria: HTTP 200.
- Grafana /api/health, Prometheus /-/ready e Jaeger: HTTP 200.
- curl.exe --silent --no-buffer --max-time 2 no SSE via Nginx: eventos price-update reais; exit 28 esperado pelo limite de observacao do stream continuo. Exemplo: VALE3, price 7421, occurredAt 2026-10-20T13:51:33.811370622Z.
- 13 servicos persistentes running; PostgreSQL/Kafka/Order/Inventory/Pricing healthy; kafka-init Exited (0).

Nao foi executado cold start, rebuild/deploy Docker, teste de navegador novo, interrupcao de servicos ou teste de carga. Os oito cenarios integrados e a reconexao no navegador pertencem a [Fase 29.2](INTEGRATION_TESTS.md), preservados como historico; nao foram repetidos aqui. Interrupcao Kafka permanece NAO EXECUTADA pelo risco R1.

Os scripts usam nomes de servico Compose, PSScriptRoot para localizar o repositorio, IDs unicos e report JSON em caminho configuravel. .cache/, node_modules/ e dist/ foram confirmados como ignorados pelo Git. Request tem timeout de 15s, Compose 60s, SSE tem janela limitada e Wait-Until padrao de 90s entre sondas (a ultima sonda pode exceder o prazo pelo seu proprio timeout).

IncludeFailures e opt-in e tem finally de restauracao, mas encerramento forcado do processo pode impedir finally; use o estado registrado e as instrucoes de retomada. Helper cobre arrays REST PowerShell 5.1 e timeout WebException; nao comprova todos os tipos de erro do HttpClient em todas as versoes do PowerShell. O runner real foi validado em Windows PowerShell 5.1 na Fase 29.2, nao novamente em todos os shells nesta revisao.

## Documentacao e arquivos

Criado: este relatorio, docs/TECHNICAL_REVIEW.md.

Modificados:
- README.md: resumo operacional, tecnologias, requisitos, acesso, testes, limites e links relativos.
- docs/ARCHITECTURE.md: diagramas reais e correcao das afirmacoes de idempotencia/instrumentacao; historico por fase preservado.
- docs/ROADMAP.md e docs/PROGRESS.md: status 29.3 e pendencias 30/31; realocacao de blocos historicos incorretos, sem apagar suas evidencias.
- docs/INTEGRATION_TESTS.md: distincao entre resultados 29.2 e revisao 29.3.
- docs/guides/RUNNING-LOCALLY.md e QUICK-REFERENCE.md: portas e comandos atuais, cuidados com volumes, proxies e modos de execucao.
- docker-compose.yml: set -e em kafka-init e dependencia frontend/Audit.
- src/frontend/MicroservicesLab.App/src/app/core/api/lab-health.service.ts: timeout health de 5s.
- src/frontend/MicroservicesLab.App/src/app/core/api/api-services.spec.ts: testes direcionados da correcao.

## Pendencias preservadas

Fase 30: layout responsivo, navegacao mobile, tabelas/grids, formularios e ajustes visuais em desktop/tablet/celular.
Fase 31: consistencia pedidos/estoque, reserva atomica, idempotencia, concorrencia, compensacao e falhas distribuidas.
Demais riscos ficam registrados acima, sem criar fases novas ou antecipar funcionalidades. Fases 17-22 nao foram incorporadas.

Nao executados git add, git commit ou git push. Branch nao alterada; commits ficam a cargo do usuario. Parada ao final da Fase 29.3.
