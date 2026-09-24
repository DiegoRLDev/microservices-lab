# Arquitetura do MicroservicesLab

Este documento registra as principais decisoes arquiteturais do laboratorio.

O laboratorio agora esta organizado em duas partes:

- Parte I - Microservices Fundamentals: Fases 0-12.
- Parte II - Realtime Distributed Systems: Fases 13-22, somente depois da conclusao da Parte I.

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

### Realtime.PriceFeed

Servico planejado para a Parte II, nao implementado na Parte I.

Sera escrito em Go para estudar processamento realtime, goroutines, channels, streaming, conexoes longas, alto volume de mensagens, baixo overhead e integracao com Kafka. A escolha de Go aqui e intencional: esse workload tem caracteristicas diferentes dos servicos de negocio da Parte I.

O servico gerara ou recebera atualizacoes de precos, publicara `PriceUpdated` no Kafka e distribuira atualizacoes a clientes via WebSocket ou Server-Sent Events.

### Pricing.Service

Servico planejado para a Parte II, nao implementado na Parte I.

Sera escrito em .NET e consumira eventos `PriceUpdated` publicados pelo `Realtime.PriceFeed`. O objetivo principal sera demonstrar interoperabilidade entre Go e .NET atraves de contratos independentes de linguagem e Kafka.

## Dono dos Dados

Cada servico controla suas proprias tabelas ou banco:

| Servico | Dados |
| --- | --- |
| Order.Service | `Order` |
| Inventory.Service | `Product` |
| Payment.Service | sem persistencia propria na FASE 8 |
| Audit.Worker | `AuditEvent` em `audit_db` |
| Realtime.PriceFeed | eventos de preco em memoria/stream, planejado para a Parte II |
| Pricing.Service | ultimo preco conhecido por simbolo, planejado para a Parte II |

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

Na Parte II, Kafka tambem sera usado para demonstrar interoperabilidade entre Go e .NET com o topico `price-updated`. O produtor Go nao dependera de tipos .NET, e o consumidor .NET nao dependera de tipos Go. A integracao acontecera pelo contrato do evento.

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

Para a expansao realtime, o evento inicial planejado e:

```json
{
  "eventId": "uuid",
  "eventType": "PriceUpdated",
  "symbol": "PETR4",
  "price": 32.45,
  "currency": "BRL",
  "occurredAt": "2026-08-31T12:00:00Z",
  "correlationId": "uuid"
}
```

Esse contrato podera ser refinado na FASE 15, mas devera permanecer independente de linguagem.

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

Na Parte II, a observabilidade sera expandida para medir eventos por segundo, erros, latencia, Kafka consumer lag, CPU, memoria e quantidade de conexoes realtime.

## Kubernetes

Na FASE 11, Kubernetes foi implementado para o ambiente local usando Docker Desktop Kubernetes.

O cluster usa o namespace `microservices-lab` e manifests em `k8s/`. A comunicacao interna usa Services `ClusterIP` e DNS interno: `postgres`, `kafka`, `inventory-service`, `order-service`, `otel-collector`, `jaeger`, `prometheus` e `grafana`.

PostgreSQL e Kafka rodam dentro do cluster para a finalidade didatica da fase. Ambos usam PVCs locais simples, sem alta disponibilidade. Kafka continua em modo KRaft single-node, e os topicos `order-created`, `payment-approved` e `payment-rejected` sao criados por um Kubernetes Job `kafka-init`, cujo estado `Completed` e esperado.

`Order.Service` nao conhece o IP dos Pods de Inventory. Ele usa `InventoryGrpc__Address=http://inventory-service:8081`, e o Kubernetes resolve o Service para as replicas disponiveis.

`Inventory.Service` foi usado para demonstrar replicas, self-healing e rolling update. Ao apagar manualmente um Pod, o Deployment criou outro para restaurar o desired state. O rolling update foi validado trocando a imagem para `microservices-lab-inventory-service:phase11-v2` sem derrubar todas as replicas simultaneamente.

Na Parte II, Kubernetes sera usado para estudar escalabilidade horizontal de `Realtime.PriceFeed` e `Pricing.Service`. Um ponto importante sera relacionar replicas, partitions Kafka e consumer groups, mostrando por que aumentar replicas nem sempre aumenta throughput.

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

Na expansao realtime, tambem sera estudado o problema de multiplas instancias de um ingester lendo a mesma fonte externa. O laboratorio devera comparar alternativas como leader election, distributed lock, particionamento da fonte, separacao entre ingestion e processing e idempotencia, mas sem implementar todas de uma vez.

## Expansao Realtime Planejada

### FASE 13 - Realtime.PriceFeed

Criar `src/services/Realtime.PriceFeed/` em Go para gerar ou receber atualizacoes de precos, processar dados continuamente, usar goroutines e channels quando fizer sentido, expor health checks, logs estruturados e graceful shutdown.

Documentar as goroutines, responsabilidades, channels, ciclo de vida e cancelamento com `context.Context`.

### FASE 14 - Streaming para clientes

Adicionar WebSocket ou Server-Sent Events. A escolha inicial deve favorecer simplicidade didatica para visualizar atualizacoes sem polling HTTP manual.

### FASE 15 - Kafka PriceUpdated

Publicar `PriceUpdated` no topico `price-updated`, estudando producer, partitions, message key, ordering, throughput, serializacao, retries e delivery semantics.

`symbol` sera o candidato inicial a message key, porque preserva ordenacao por ativo dentro de uma partition, mas tambem concentra carga se poucos simbolos forem muito ativos.

### FASE 16 - Pricing.Service em .NET

Criar `Pricing.Service` para consumir `price-updated` produzido pelo servico Go e manter o ultimo preco conhecido por simbolo, com endpoints `GET /prices/{symbol}` e `GET /prices`.

### FASE 17 - Concorrencia e backpressure

Criar experimentos com volumes como 100, 1.000 e 10.000 eventos por segundo, ajustando conforme o ambiente local. O objetivo e observar o comportamento quando producao e maior que capacidade de consumo.

### FASE 18 - Escalabilidade no Kubernetes

Executar `Realtime.PriceFeed` e `Pricing.Service` no Kubernetes, estudar replicas, Deployments, Services, HPA, CPU, memoria e distribuicao de carga.

### FASE 19 - Multiplas instancias PriceFeed

Criar laboratorio para observar o que acontece quando varias replicas do PriceFeed consomem a mesma fonte externa e podem gerar eventos duplicados. Escolher uma solucao adequada para o laboratorio e documentar trade-offs.

### FASE 20 - Separacao Ingester / Processor

Evoluir conceitualmente para `Price.Ingester -> Kafka price-raw -> Processors -> Kafka price-updated`, estudando separacao entre ingestion e stream processing.

### FASE 21 - Observabilidade Realtime

Instrumentar o pipeline realtime usando a infraestrutura de observabilidade criada anteriormente sempre que possivel. Criar um dashboard didatico para acompanhar o PriceFeed.

### FASE 22 - Testes de carga e caos

Criar laboratorios para aumentar volume de eventos, derrubar producer/consumer, reiniciar Kafka, remover Pods, escalar replicas, gerar consumer lag e observar recuperacao.
