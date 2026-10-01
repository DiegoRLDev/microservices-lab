# Arquitetura do MicroservicesLab

Este documento registra as principais decisoes arquiteturais do laboratorio.

O laboratorio agora esta organizado em duas partes:

- Parte I - Microservices Fundamentals: Fases 0-12.
- Parte II - Realtime Distributed Systems: Fases 13-22, iniciada depois da conclusao da Parte I.

## Servicos

### Order.Service

Responsavel por receber pedidos via HTTP REST, consultar estoque via gRPC, persistir pedidos e publicar eventos de criacao de pedido. Ele e dono dos dados de `Order`.

Na pratica, a consistencia eventual aparecera quando o pedido for salvo inicialmente como `Pending`. O status so mudara para `Paid` ou `PaymentRejected` depois que `Order.Service` consumir o evento publicado por `Payment.Service`.

Na FASE 1, o `Order.Service` existe sem as dependencias distribuidas. Ele valida `ProductId` e `Quantity`, grava o pedido como `Pending` em PostgreSQL via EF Core e expoe `POST /orders`, `GET /orders/{id}` e `GET /orders`.

Na FASE 3, o `POST /orders` consulta `Inventory.Service` via gRPC antes de persistir. `UnitPrice` vem da resposta do Inventory e `TotalPrice` e calculado dentro da entidade `Order`. Se o estoque nao estiver disponivel, o pedido nao e criado.

Na FASE 4, depois de persistir o pedido, `Order.Service` publica `OrderCreated` no Kafka. Esse evento e o ponto de entrada para o `Payment.Service` na proxima fase.

### Inventory.Service

Responsavel pelo catalogo simples de produtos e disponibilidade de estoque. Ele e dono dos dados de `Product`.

`Order.Service` nao acessara o banco de estoque diretamente. A consulta sera feita por gRPC porque e uma comunicacao sincrona, interna e orientada a baixa latencia.

Na FASE 2, `Inventory.Service` ja controla produtos e estoque em banco proprio usando EF Core/PostgreSQL. Ele expoe endpoints HTTP didaticos para cadastrar produtos, listar produtos, checar disponibilidade e reservar estoque.

Na FASE 3, `CheckAvailability` e `ReserveStock` tambem sao expostos por gRPC para uso interno por outros servicos. O `Order.Service` usa `CheckAvailability` nesta fase.

### Payment.Service

Responsavel por consumir `OrderCreated`, simular o processamento de pagamento e publicar `PaymentApproved` ou `PaymentRejected`.

Sera escrito em Go para o laboratorio exercitar interoperabilidade entre stacks mantendo contrato por evento, nao por compartilhamento de banco.

Na FASE 5, `Payment.Service` ja existe em Go e consome `OrderCreated` do Kafka. Ele desserializa o JSON usando contratos proprios em Go, valida o tipo do evento, confere a message key contra o `OrderId` e simula a decisao de pagamento em log.

Na FASE 6, `Payment.Service` publica `PaymentApproved` ou `PaymentRejected` depois da simulacao de pagamento. O evento usa `OrderId` como Kafka message key, preservando a ordenacao por pedido. Persistencia de `Payment` em `payment_db` ainda nao foi implementada.

### Audit.Worker

Responsavel por consumir eventos relevantes e registrar historico em `AuditEvent`.

Ele nao participa do fluxo transacional principal. Isso reforca a ideia de consumidor assincrono acoplado por eventos.

Na FASE 7, `Audit.Worker` consome `order-created`, `payment-approved` e `payment-rejected` com o consumer group `audit-worker`. Cada envelope recebido e gravado no banco `audit_db`, na tabela `audit_events`, preservando payload JSON, `eventId`, `eventType`, topic, Kafka key, partition, offset, `correlationId`, `occurredAt` e `auditedAt`.

### PriceFeed.Service

Servico Go implementado na FASE 13 para iniciar a Parte II.

Foi escrito em Go para estudar fluxo continuo de precos, goroutines, channels, cancelamento com `context.Context`, streaming, conexoes longas, alto volume de mensagens, baixo overhead e futuras discussoes de backpressure. Na FASE 14, o servico passou a entregar o fluxo para clientes externos via SSE. Depois dela, um mini-lab adicionou gRPC Server Streaming para comparar streaming direto tipado com SSE antes da entrada de Kafka. Na FASE 15, Kafka entrou como saida assíncrona opcional para o topico `price-updated`. A escolha de Go aqui e intencional: esse workload tem caracteristicas diferentes dos servicos de negocio da Parte I.

O servico fornece atualizacoes continuas de precos, com origem desacoplada por `PriceProvider`. A implementacao inicial e `MockPriceProvider`, mas o nucleo do servico depende apenas da interface, permitindo substituir a fonte por API externa, WebSocket externo ou market data provider em fase futura.

Responsabilidades conceituais:

- `PriceProvider`: de onde vem os precos;
- `PriceFeed`: como o servico trabalha com o fluxo continuo;
- `Publisher`: para onde os precos sao enviados. Na FASE 15, `ConsolePublisher`, `Broadcaster` e `KafkaPublisher` recebem o mesmo fluxo por um `MultiPublisher`; SSE e gRPC assinam o `Broadcaster`, enquanto Kafka recebe eventos `PriceUpdated`.

### Pricing.Service

Servico .NET iniciado como cliente didatico no mini-lab de gRPC Server Streaming e evoluido na FASE 16 para consumir `price-updated` via Kafka.

Na FASE 16, Kafka passa a ser o mecanismo principal para alimentar o estado atual do servico. O `KafkaPriceUpdatedConsumer` consome eventos `PriceUpdated`, valida o contrato JSON, registra divergencia entre Kafka key e `symbol` quando ela aparece e atualiza `LatestPriceStore`.

O estado e mantido em memoria por simbolo, usando `ConcurrentDictionary`. Essa escolha e suficiente para o laboratorio porque existe escrita pelo consumer Kafka e leitura simultanea pelos endpoints HTTP, sem exigir banco, Redis, CQRS ou Event Sourcing.

Endpoints:

```text
GET /health
GET /prices
GET /prices/{symbol}
```

O mini-lab gRPC continua preservado por configuracao (`GrpcLab:Enabled=true`). Nesse modo, o servico abre uma chamada `StreamPrices` contra o `PriceFeed.Service` e imprime o fluxo recebido, mas esse caminho nao alimenta o estado REST da FASE 16.

## Dono dos Dados

Cada servico controla suas proprias tabelas ou banco:

| Servico | Dados |
| --- | --- |
| Order.Service | `Order` |
| Inventory.Service | `Product` |
| Payment.Service | sem persistencia propria na FASE 8 |
| Audit.Worker | `AuditEvent` em `audit_db` |
| PriceFeed.Service | eventos de preco em memoria/stream, sem persistencia propria |
| Pricing.Service | ultimo preco conhecido por simbolo em memoria, derivado do stream Kafka `price-updated` |

Nenhum servico acessara tabelas de outro servico. Essa regra evita acoplamento por banco, que costuma parecer simples no inicio e caro quando a arquitetura precisa evoluir.

## REST vs gRPC vs Kafka

REST sera usado na borda do sistema porque e simples de testar com `curl`, Postman e ferramentas comuns.

gRPC sera usado entre `Order.Service` e `Inventory.Service` porque a verificacao de disponibilidade faz parte da decisao imediata de criar o pedido.

As operacoes que cruzam o limite entre servicos sao `CheckAvailability` e `ReserveStock`. A FASE 2 implementou essas regras dentro do servico dono do estoque; a FASE 3 expôs o transporte interno por gRPC sem mudar a regra de dominio.

O contrato fica em `src/protos/inventory.proto`, fora dos projetos individuais, porque o contrato pertence a fronteira entre servicos. Isso evita que o `Order.Service` precise referenciar assemblies internos do `Inventory.Service`.

Kafka sera usado para propagacao de eventos entre servicos. O pagamento nao precisa bloquear a resposta inicial do pedido; por isso, `OrderCreated`, `PaymentApproved` e `PaymentRejected` serao eventos assincronos.

Na FASE 4, o primeiro evento publicado e `OrderCreated` no topico `order-created`. O produtor usa `OrderId` como message key. Isso preserva a ordenacao dos eventos de um mesmo pedido dentro de uma partition, mas nao cria ordenacao global entre pedidos diferentes.

Na FASE 4.1, o Kafka e executado isoladamente via `infra/kafka/docker-compose.kafka.yml` para validar o producer real antes de introduzir o `Payment.Service`. Essa etapa nao substitui a FASE 8: ela sobe apenas Kafka, enquanto a FASE 8 sera responsavel por toda a arquitetura em Docker Compose.

Na FASE 5, `Payment.Service` entra como consumidor Kafka do topico `order-created` usando o consumer group `payment-service`. Ele commita a mensagem depois de processar a simulacao local.

Na FASE 6, `Payment.Service` tambem atua como producer Kafka para `payment-approved` e `payment-rejected`. `Order.Service` assina esses dois topicos usando o consumer group `order-service-payment-events` e atualiza o status do pedido para `Paid` ou `PaymentRejected`.

Na FASE 7, `Audit.Worker` assina os tres topicos ativos usando o consumer group `audit-worker` e registra os envelopes em `audit_db`. O worker commita a mensagem depois de persistir o registro de auditoria ou reconhecer que o `eventId` ja foi gravado.

Na FASE 8, o ambiente completo roda via `docker compose up -d --build`. Dentro da rede Docker, os servicos usam DNS interno: `postgres:5432`, `kafka:29092` e `inventory-service:8081` para gRPC. Para inspecao pelo host, as portas preservadas sao `5432` para PostgreSQL, `9092` para Kafka, `5276` para `Inventory.Service`, `7242` para gRPC do Inventory e `5074` para `Order.Service`.

O Compose principal cria topicos Kafka por meio do servico `kafka-init` e usa volumes nomeados `postgres_data` e `kafka_data`. As migrations EF Core de `Order.Service`, `Inventory.Service` e `Audit.Worker` sao aplicadas na inicializacao quando `Database__ApplyMigrations=true` esta configurado.

Na Parte II, Kafka tambem e usado para demonstrar interoperabilidade entre Go e .NET com o topico `price-updated`. Na FASE 14, `PriceFeed.Service` publica no console local e entrega o fluxo via SSE em `GET /prices/stream`. No mini-lab intermediario, o mesmo fluxo tambem e exposto por gRPC Server Streaming usando `src/protos/pricefeed.proto`. Na FASE 15, o produtor Go publica `PriceUpdated` no Kafka sem conhecer consumidores. Na FASE 16, `Pricing.Service` consome esse JSON sem depender de tipos Go e constroi estado derivado atual.

Na FASE 15, `symbol` e a message key do Kafka para estudar ordenacao por ativo dentro de uma partition. Kafka preserva ordering dentro da partition, mas nao fornece ordering global entre partitions. Essa escolha tambem devera ser observada futuramente do ponto de vista de distribuicao de carga, porque keys muito concentradas podem limitar paralelismo.

## Envelope de Eventos

Os eventos seguirao um envelope comum:

```json
{
  "eventId": "guid",
  "eventType": "OrderCreated",
  "occurredAt": "2026-08-31T12:00:00Z",
  "correlationId": "trace-or-request-id",
  "data": {}
}
```

Esse envelope facilita auditoria, idempotencia, logs estruturados e correlacao distribuida.

Na FASE 4, `OrderCreated` usa esse envelope com `OrderCreatedData`. O contrato .NET fica em `src/building-blocks/Shared.Contracts`, enquanto a forma serializada no Kafka e JSON para permitir que o `Payment.Service` em Go consuma sem depender de assemblies .NET.

Na FASE 5, o contrato consumido pelo Go e definido localmente em `src/services/Payment.Service/internal/contracts`. Essa duplicacao e intencional: a fronteira compartilhada entre stacks e o JSON no Kafka, nao uma biblioteca binaria comum.

Na FASE 6, `Shared.Contracts` passa a registrar `PaymentApprovedData` e `PaymentRejectedData` para o consumidor .NET. O `Payment.Service` Go define structs equivalentes localmente e publica JSON no mesmo envelope.

Na FASE 7, o `Audit.Worker` nao precisa conhecer os tipos concretos de `data` para auditar. Ele le os campos comuns do envelope e armazena o JSON completo em `payload`.

Para a expansao realtime, o evento implementado na FASE 15 e:

```json
{
  "symbol": "PETR4",
  "price": 3815,
  "occurredAt": "2026-09-29T14:30:00Z"
}
```

O contrato e simples de proposito: `price` permanece em centavos (`int64`), e o evento nao usa envelope completo nesta fase. A meta e estudar stream, key, partitions, desacoplamento temporal e estado derivado sem introduzir Schema Registry ou Event Sourcing.

## Idempotencia

Os consumidores Kafka manterao um registro simples de eventos processados, usando `EventId` como chave. Antes de aplicar efeito colateral, o consumidor verificara se o evento ja foi processado.

No laboratorio, isso sera propositalmente didatico: uma tabela local por consumidor e suficiente para mostrar o conceito sem introduzir uma infraestrutura mais complexa.

Na FASE 7, essa regra ja aparece no `Audit.Worker`: `audit_events.event_id` e unico, e o processador ignora mensagens cujo `EventId` ja tenha sido registrado.

## Resiliencia

A chamada `Order.Service -> Inventory.Service` tem timeout, retry limitado e circuit breaker. Essas politicas existem porque o estoque e uma dependencia sincrona: se ela estiver lenta ou indisponivel, a criacao de pedido deve falhar de forma controlada.

O objetivo nao e mascarar falhas indefinidamente, mas tornar o comportamento previsivel para estudo.

Na FASE 9, `GrpcInventoryClient` aplica a politica de resiliencia somente em `CheckAvailability`, que e a operacao usada pelo fluxo de criacao de pedido. Produto inexistente continua sendo tratado como regra de negocio: retorna estoque indisponivel e nao conta como falha transiente.

Quando o `Inventory.Service` esta indisponivel, `Order.Service` retorna HTTP `503 Service Unavailable`, nao persiste o pedido e nao publica `OrderCreated`. Apos falhas consecutivas, o circuit breaker abre e as proximas chamadas falham rapido. Depois da janela configurada, uma chamada de teste entra em half-open; se tiver sucesso, o circuito fecha.

Falhas Kafka continuam como trade-off da arquitetura atual. Se o pedido for salvo e a publicacao de `OrderCreated` falhar, pode existir pedido `Pending` sem evento correspondente. A FASE 9 documenta esse comportamento e deixa Outbox Pattern para uma fase futura.

## Observabilidade

Na FASE 10, o projeto foi instrumentado com OpenTelemetry para correlacionar uma requisicao `POST /orders` com:

- chamada HTTP de entrada;
- chamada gRPC para estoque;
- operacoes de banco;
- publicacao e consumo de eventos Kafka;
- logs estruturados com `TraceId`, `OrderId`, `ProductId`, `PaymentId` e `EventId`.

O ambiente Docker Compose inclui OpenTelemetry Collector, Jaeger, Prometheus e Grafana. Os servicos exportam traces e metricas via OTLP, e o contexto distribuido atravessa Kafka por headers W3C `traceparent`/`tracestate`, sem alterar o payload dos eventos.

O dashboard provisionado em Grafana acompanha requisicoes HTTP, duracao, pedidos criados/rejeitados, pagamentos aprovados/rejeitados e falhas da dependencia Inventory.

Na Parte II, a observabilidade sera expandida para responder perguntas especificas de streaming: quantos eventos por segundo entram, quantos sao processados, qual e a latencia, se o consumer lag cresce, se existe pressao em buffers/channels, se eventos estao sendo descartados e quantos clientes realtime estao conectados.

## Kubernetes

Na FASE 11, Kubernetes foi implementado para o ambiente local usando Docker Desktop Kubernetes.

O cluster usa o namespace `microservices-lab` e manifests em `k8s/`. A comunicacao interna usa Services `ClusterIP` e DNS interno: `postgres`, `kafka`, `inventory-service`, `order-service`, `otel-collector`, `jaeger`, `prometheus` e `grafana`.

PostgreSQL e Kafka rodam dentro do cluster para a finalidade didatica da fase. Ambos usam PVCs locais simples, sem alta disponibilidade. Kafka continua em modo KRaft single-node, e os topicos `order-created`, `payment-approved` e `payment-rejected` sao criados por um Kubernetes Job `kafka-init`, cujo estado `Completed` e esperado.

`Order.Service` nao conhece o IP dos Pods de Inventory. Ele usa `InventoryGrpc__Address=http://inventory-service:8081`, e o Kubernetes resolve o Service para as replicas disponiveis.

`Inventory.Service` foi usado para demonstrar replicas, self-healing e rolling update. Ao apagar manualmente um Pod, o Deployment criou outro para restaurar o desired state. O rolling update foi validado trocando a imagem para `microservices-lab-inventory-service:phase11-v2` sem derrubar todas as replicas simultaneamente.

Na Parte II, Kubernetes podera ser usado para materializar replicas de consumers, mas nao sera o conceito principal das fases 18 e 19. O foco sera relacionar replicas, partitions Kafka, consumer groups, partition assignment, rebalance e ordering, mostrando por que aumentar replicas alem do numero de partitions nao aumenta indefinidamente o paralelismo dentro do mesmo consumer group.

## Fechamento da Parte I

A Parte I termina com dois modos operacionais preservados:

- Docker Compose completo para execucao local rapida;
- Kubernetes local para estudar Deployments, Services, ConfigMaps, Secrets, PVCs, probes, replicas, self-healing e rolling update.

A arquitetura implementada ate a FASE 12 demonstra:

- HTTP REST na borda;
- gRPC para dependencia sincrona interna;
- Kafka para eventos assincronos;
- bancos PostgreSQL separados por ownership;
- consumidor Go interoperando com produtores/consumidores .NET por JSON;
- auditoria assincrona;
- resiliencia no caminho sincrono;
- observabilidade com OpenTelemetry, Jaeger, Prometheus e Grafana.

Limites intencionais que ficam para evolucao futura:

- Outbox Pattern para garantir atomicidade entre banco e Kafka;
- DLQ/retry topics para consumidores Kafka;
- banco proprio para `Payment.Service`;
- Ingress, TLS, HPA, Service Mesh, Operators e GitOps no Kubernetes;
- autenticacao/autorizacao entre servicos;
- logs centralizados fora do stdout;
- pipeline realtime da Parte II.

## Trade-offs

Este projeto evita CQRS completo, Event Sourcing, Service Mesh e abstracoes genericas prematuras. A meta e estudar comunicacao, consistencia eventual, operacao e resiliencia sem transformar um laboratorio pequeno em uma plataforma corporativa pesada.

Na FASE 1, a regra de criacao de pedido fica em `OrdersAppService` em vez de ser colocada diretamente nos endpoints. Isso mantem a API fina e permite testar a regra sem subir HTTP nem PostgreSQL real. Ainda nao ha repository interface porque o `DbContext` ja e uma unidade de trabalho adequada para este escopo.

Na FASE 2, `InventoryAppService` segue o mesmo desenho: endpoints finos, regra de negocio no servico de aplicacao e persistencia via `InventoryDbContext`. A reserva de estoque altera apenas o banco do Inventory, preservando a regra de que nenhum servico escreve no banco de outro.

Na FASE 3, o `Order.Service` depende de `IInventoryClient`, uma porta pequena que esconde o client gRPC gerado. Essa escolha torna a regra de criacao de pedido testavel sem subir rede, ao mesmo tempo que mantem o transporte real na camada de infraestrutura.

Na FASE 4, a mesma ideia e aplicada ao Kafka com `IIntegrationEventPublisher`. A regra de criacao de pedido publica um evento sem conhecer `Confluent.Kafka` diretamente. O produtor concreto usa `acks=all` e idempotencia habilitada no client Kafka.

O evento e publicado depois do `SaveChanges`. Essa escolha e simples para estudar Kafka cedo. O trade-off e que ainda nao temos garantia atomica entre banco e broker: se o banco salvar e a publicacao falhar, pode haver inconsistencia. Um outbox pattern resolveria isso, mas fica fora desta fase para preservar o foco.

Na FASE 4.1, esse problema fica documentado explicitamente:

```text
SaveChanges()
    |
    v
SUCESSO

Publish Kafka
    |
    v
FALHA
```

Resultado:

```text
OrdersDb: pedido existe
Kafka: OrderCreated nao existe
```

Esse e um limite importante da arquitetura atual: PostgreSQL e Kafka nao participam da mesma transacao. O Outbox Pattern podera ser usado futuramente para persistir o evento junto com a alteracao do banco e publica-lo de forma confiavel, mas ele nao sera implementado nesta etapa.

Na expansao realtime, tambem sera estudado o problema de multiplas instancias trabalhando sobre streams particionados. A FASE 18 observara consumer scaling em consumer groups; a FASE 19 aprofundara partitioning, ownership e ordering. O laboratorio podera comparar alternativas como leader election, distributed lock, particionamento da fonte, separacao entre ingestion e processing e idempotencia, mas sem implementar todas de uma vez.

## Expansao Realtime Planejada

### FASE 13 - PriceFeed.Service

Implementada. O servico Go fornece um fluxo continuo de precos ao sistema, com `PriceProvider` desacoplado da logica principal. A implementacao inicial e `MockPriceProvider`, com preco inicial, pequenas oscilacoes, multiplos simbolos e intervalo configuravel.

Cada simbolo simulado roda em uma goroutine produtora. As goroutines enviam `PriceUpdate` para um channel compartilhado, consumido por `PriceFeed`. O channel e fechado pelo provider depois que todos os produtores param. O ciclo de vida e controlado por `context.Context`, inclusive no encerramento por `Ctrl+C`.

Os precos sao representados em centavos (`int64`). A decisao evita problemas basicos de arredondamento de `float64` e evita uma dependencia decimal externa desnecessaria para esta fase didatica.

### FASE 14 - Realtime Delivery - SSE vs WebSocket

Implementada com SSE. A escolha foi feita porque o fluxo atual e principalmente backend -> cliente, sem requisito de comunicacao bidirecional continua. WebSocket poderia atender tecnicamente, mas adicionaria complexidade que ainda nao e necessaria.

O endpoint `GET /prices/stream` responde com `Content-Type: text/event-stream` e envia eventos `price-update` com JSON no campo `data`:

```text
event: price-update
data: {"symbol":"PETR4","price":3810,"occurredAt":"2026-09-28T14:30:01Z"}
```

O `Broadcaster` mantem subscribers independentes para que varios clientes recebam o mesmo fluxo sem competir pelo channel do provider. Cada subscriber tem um channel bufferizado. Se um cliente ficar lento e o buffer encher, novas atualizacoes para aquele subscriber sao descartadas; isso evita que um unico cliente bloqueie todos os demais. A FASE 17 aprofundara backpressure.

O ciclo de vida do subscriber e ligado ao request context: quando o cliente desconecta, o contexto da requisicao e cancelado, o subscriber e removido e o channel e fechado.

### Mini-lab - gRPC Server Streaming

Implementado depois da FASE 14 e antes da FASE 15. O objetivo e comparar streaming direto tipado com SSE, sem introduzir broker ainda.

O contrato fica em `src/protos/pricefeed.proto`:

```text
service PriceFeed {
  rpc StreamPrices(StreamPricesRequest) returns (stream PriceUpdate);
}
```

O adaptador Go `grpcdelivery` assina o `Broadcaster`, converte `PriceUpdate` para o contrato Protobuf e envia mensagens no stream gRPC. O nucleo do `PriceFeed` continua sem conhecer gRPC, SSE, .NET ou Kafka.

O `Pricing.Service` preserva o cliente .NET de laboratorio como modo opcional. Com `GrpcLab:Enabled=true`, ele conecta em `GrpcLab:PriceFeedAddress` ou `PRICE_FEED_GRPC_ADDRESS`, chama `StreamPrices`, imprime as atualizacoes e respeita cancelamento por `Ctrl+C`.

Resumo:

```text
MockPriceProvider
      |
      v
PriceFeed
      |
      v
MultiPublisher
      |
      +--> ConsolePublisher
      |
      +--> Broadcaster
              |
              +--> SSE clients
              |
              +--> gRPC StreamPrices clients
```

Kafka permanece fora deste mini-lab. A FASE 15 e responsavel por `PriceUpdated` em broker, partitions, key e delivery semantics.

### FASE 15 - Kafka PriceUpdated

Implementada. `KafkaPublisher` e um adapter de `Publisher` no `PriceFeed.Service`, configurado por:

```text
PRICE_KAFKA_ENABLED
PRICE_KAFKA_BOOTSTRAP_SERVERS
PRICE_KAFKA_TOPIC
```

O topico `price-updated` tem 3 partitions e replication factor 1 no ambiente single-node. A criacao continua idempotente junto com `order-created`, `payment-approved` e `payment-rejected`.

Fluxo:

```text
MockPriceProvider
      |
      v
PriceFeed
      |
      v
MultiPublisher
      |
      +--> ConsolePublisher
      |
      +--> Broadcaster
      |       |
      |       +--> SSE clients
      |       +--> gRPC StreamPrices clients
      |
      +--> KafkaPublisher
              |
              v
         price-updated
```

Falhas de escrita Kafka sao registradas em log e nao derrubam o feed. A decisao e didatica: console, SSE e gRPC continuam funcionando enquanto Kafka indisponivel fica visivel. Retry sofisticado, outbox, DLQ e exactly-once ficam fora desta fase.

`symbol` e a message key, porque preserva ordenacao por ativo dentro de uma partition, mas tambem pode concentrar carga se poucos simbolos forem muito ativos.

### FASE 16 - Pricing.Service em .NET

Implementada. `Pricing.Service` consome `price-updated` produzido pelo servico Go e mantem o ultimo preco conhecido por simbolo, com endpoints `GET /prices/{symbol}` e `GET /prices`.

Fluxo:

```text
PriceFeed.Service
        |
        | PriceUpdated
        v
      Kafka
   price-updated
        |
        v
KafkaPriceUpdatedConsumer
        |
        v
LatestPriceStore
        |
        +-- GET /prices
        +-- GET /prices/{symbol}
```

Configuracao principal:

```text
Kafka:Enabled
Kafka:BootstrapServers
Kafka:Topic
Kafka:ConsumerGroup
GrpcLab:Enabled
GrpcLab:PriceFeedAddress
```

No Docker Compose, `pricing-service` usa `kafka:29092`, consumer group `pricing-service`, topico `price-updated` e porta host `5090`.

O gRPC Server Streaming continua didatico e direto:

```text
PriceFeed.Service -------- conexao direta --------> Pricing.Service
```

Quando `Pricing.Service` esta offline, nao ha cliente conectado naquele periodo. Com Kafka, o produtor e o consumidor ficam temporalmente desacoplados:

```text
PriceFeed.Service -> Kafka -> Pricing.Service
```

O consumer usa `AutoOffsetReset=Earliest` e commits manuais apos processar a mensagem. Para um consumer group novo, isso permite iniciar pelo inicio do log retido. Para um consumer group existente, a retomada segue os offsets ja commitados pelo Kafka.

### FASE 17 - Concorrencia e backpressure

Criar experimentos com volumes como 100, 1.000 e 10.000 eventos por segundo, ajustando conforme o ambiente local. O objetivo e observar o comportamento quando producao e maior que capacidade de consumo.

Relacionar conceitos Go como goroutines, buffered channels, unbuffered channels, blocking, worker pool, fan-out/fan-in e buffer cheio com conceitos Kafka como producer throughput, consumer throughput e consumer lag.

### FASE 18 - Consumer scaling

Estudar como o numero de consumers se relaciona com o numero de partitions dentro de um mesmo consumer group. Kubernetes pode fornecer replicas, mas o conceito principal e Kafka consumer scaling.

### FASE 19 - Multiplas instancias e partitioning

Aprofundar como Kafka distribui trabalho e preserva ordenacao quando existem multiplas instancias. Trabalhar `key`, `partition`, `consumer group`, `partition ownership`, `ordering` e `rebalance`, usando `symbol` como exemplo natural.

### FASE 20 - Separacao Ingester / Processor

Evoluir conceitualmente para `Price.Ingester -> Kafka price-raw -> Processors -> Kafka price-updated`, estudando separacao entre ingestion e stream processing.

### FASE 21 - Observabilidade Realtime

Instrumentar o pipeline realtime usando a infraestrutura de observabilidade criada anteriormente sempre que possivel. Criar um dashboard didatico para acompanhar eventos recebidos/processados, eventos por segundo, latencia, consumer lag, uso de buffer/channel, eventos descartados e conexoes realtime ativas.

### FASE 22 - Testes de carga e caos

Criar laboratorios para aumentar volume de eventos, derrubar producer/consumer, reiniciar Kafka, remover Pods, escalar/reduzir replicas, gerar consumer lag e observar degradacao e recuperacao. A pergunta deixa de ser "funciona?" e passa a ser "ate onde funciona e como falha?".
