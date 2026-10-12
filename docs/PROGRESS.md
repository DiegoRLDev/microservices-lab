# Progresso

O laboratorio esta organizado em duas partes:

- Parte I - Microservices Fundamentals: Fases 0-12.
- Parte II - Realtime Distributed Systems: Fases 13-22, iniciada na FASE 13 depois do fechamento da Parte I.
- Parte III - Application / Front-end: Fases 23-29, iniciada na FASE 23.

## Parte I - Microservices Fundamentals

| Fase | Descricao | Status |
| ---- | --------- | ------ |
| 0 | Estrutura | Concluido |
| 1 | Order Service | Concluido |
| 2 | Inventory Service | Concluido |
| 3 | gRPC | Concluido |
| 4 | Kafka producer | Concluido |
| 4.1 | Validacao Kafka real | Concluido |
| 5 | Payment Go | Concluido |
| 6 | Eventos pagamento | Concluido |
| 7 | Audit Worker | Concluido |
| 8 | Docker Compose | Concluido |
| 9 | Resiliencia | Concluido |
| 10 | Observabilidade | Concluido |
| 11 | Kubernetes | Concluido |
| 12 | Labs/documentacao | Concluido |

## Parte II - Realtime Distributed Systems

| Fase | Descricao | Status |
| ---- | --------- | ------ |
| 13 | PriceFeed.Service Go | Concluido |
| 14 | Realtime Delivery - SSE vs WebSocket | Concluido |
| Mini-lab | gRPC Server Streaming Go -> .NET | Concluido |
| 15 | Kafka PriceUpdated stream | Concluido |
| 16 | Pricing.Service .NET | Concluido |
| 17 | Concorrencia e backpressure | Pendente |
| 18 | Consumer scaling | Pendente |
| 19 | Multiplas instancias e partitioning | Pendente |
| 20 | Ingester/Processor | Pendente |
| 21 | Realtime observability | Pendente |
| 22 | Load e chaos testing | Pendente |

## Parte III - Application / Front-end

| Fase | Descricao | Status |
| ---- | --------- | ------ |
| 23 | Angular, estrutura inicial e Docker Compose | Concluido |
| 24 | Dashboard e navegacao | Concluido |
| 25 | Produtos e estoque | Concluido |
| 26 | Pedidos e pagamentos | Concluido |
| 27 | Precos realtime (SSE) | Concluido |
| 28 | Auditoria e observabilidade | Em execucao |
| 29 | Integracao e validacao final | Pendente |

## Regra de execucao

A Parte I esta concluida. A Parte II foi iniciada pela FASE 13 e continua sendo executada uma fase por vez, somente por instrucao explicita.

## FASE 0 - Estrutura

Status: Concluido

Criado:

- estrutura principal de diretorios;
- arquivo de solucao `MicroservicesLab.sln`;
- documentacao inicial;
- placeholders para manter pastas vazias no controle de versao.

Validacao:

```powershell
dotnet build .\MicroservicesLab.sln
```

## Expansao registrada

A Parte II adicionou `PriceFeed.Service` em Go para estudar fluxo continuo de dados, concorrencia, throughput, backpressure, particionamento, consumer lag, escalabilidade e comportamento sob carga/falhas.

O `Pricing.Service` em .NET agora consome `PriceUpdated` e demonstra interoperabilidade Go -> Kafka -> .NET usando contratos independentes de linguagem.

A Parte II sera organizada em quatro blocos conceituais:

- Realtime: Fases 13-16;
- Concorrencia e distribuicao: Fases 17-19;
- Arquitetura: Fase 20;
- Operacao: Fases 21-22.

Na FASE 13, a origem dos precos ficou desacoplada por uma abstracao conceitual `PriceProvider`, com `MockPriceProvider` como implementacao inicial. Na FASE 15, `symbol` passou a ser a key Kafka para estudar ordenacao por ativo. Na FASE 18, o foco sera consumer scaling em consumer groups; na FASE 19, o foco sera partitioning, ownership e ordering com multiplas instancias.

## FASE 1 - Order.Service

Status: Concluido

Criado:

- `Order.Service` com ASP.NET Core Web API em .NET 9;
- entidade `Order` e enum `OrderStatus`;
- `OrdersDbContext` com mapeamento EF Core para PostgreSQL;
- migration inicial criando a tabela `orders`;
- endpoints `POST /orders`, `GET /orders/{id}` e `GET /orders`;
- health checks `/health`, `/health/live` e `/health/ready`;
- testes unitarios/de aplicacao usando EF Core InMemory.

Decisao didatica:

- nesta fase, `UnitPrice` e `TotalPrice` sao persistidos como `0`, porque o preco e a disponibilidade virao do `Inventory.Service` nas Fases 2 e 3;
- Kafka ainda nao foi introduzido, entao `OrderCreated` fica para a FASE 4.

Validacao:

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln
```

Migration:

```powershell
dotnet ef database update --project .\src\services\Order.Service\Order.Service.csproj --startup-project .\src\services\Order.Service\Order.Service.csproj
```

## FASE 2 - Inventory.Service

Status: Concluido

Criado:

- `Inventory.Service` com ASP.NET Core Web API em .NET 9;
- entidade `Product`;
- `InventoryDbContext` com mapeamento EF Core para PostgreSQL;
- migration inicial criando a tabela `products`;
- endpoints `POST /products`, `GET /products/{id}`, `GET /products`, `POST /products/{id}/availability` e `POST /products/{id}/reserve`;
- health checks `/health`, `/health/live` e `/health/ready`;
- testes unitarios/de aplicacao usando EF Core InMemory.

Decisao didatica:

- `Inventory.Service` e o unico dono de `Product` e `AvailableQuantity`;
- disponibilidade e reserva foram implementadas primeiro como regra local do servico;
- gRPC ainda nao foi introduzido para manter a fase focada no dono do estoque. A FASE 3 expora essas operacoes para `Order.Service`.

Validacao:

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln
```

Migration:

```powershell
dotnet ef database update --project .\src\services\Inventory.Service\Inventory.Service.csproj --startup-project .\src\services\Inventory.Service\Inventory.Service.csproj
```

## FASE 3 - gRPC

Status: Concluido

Criado:

- contrato compartilhado `src/protos/inventory.proto`;
- servidor gRPC `InventoryGrpcService` no `Inventory.Service`;
- client gRPC tipado no `Order.Service`;
- porta `IInventoryClient` para manter a regra de pedido testavel;
- tratamento simples para produto inexistente, estoque insuficiente e indisponibilidade do Inventory;
- teste de criacao de pedido com estoque indisponivel.

Decisao didatica:

- `Order.Service` usa gRPC para uma consulta sincrona interna que participa da decisao de criar ou nao o pedido;
- o contrato `.proto` fica fora dos servicos para representar uma fronteira independente;
- retry, timeout e circuit breaker ainda nao foram adicionados, porque pertencem a FASE 9 de resiliencia;
- `ReserveStock` ja esta exposto por gRPC no Inventory, mas o fluxo de pedido usa apenas `CheckAvailability` nesta fase.

Validacao:

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
```

Fluxo testavel:

```powershell
dotnet run --project .\src\services\Inventory.Service\Inventory.Service.csproj --launch-profile https
dotnet run --project .\src\services\Order.Service\Order.Service.csproj --launch-profile http
```

Depois de cadastrar um produto no Inventory, use o `ProductId` retornado em `POST /orders`.

## FASE 4 - Kafka

Status: Concluido

Criado:

- `Shared.Contracts` como biblioteca .NET para contratos compartilhados;
- envelope `EventEnvelope<TData>`;
- contrato `OrderCreatedData`;
- constantes de topicos Kafka;
- producer Kafka concreto no `Order.Service`;
- porta `IIntegrationEventPublisher` para manter a regra testavel;
- configuracao `Kafka:BootstrapServers` e `Kafka:OrderCreatedTopic`;
- documentacao e script inicial para criacao de topicos em `infra/kafka`.

Decisao didatica:

- `Order.Service` publica `OrderCreated` depois de persistir o pedido;
- o evento e JSON para permitir interoperabilidade futura com Go;
- `OrderId` e usado como message key;
- producer usa `acks=all` e idempotencia no client Kafka;
- ainda nao ha consumidor Kafka nesta fase;
- ainda nao ha outbox pattern, entao a atomicidade banco + Kafka sera discutida como trade-off.

Validacao:

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
```

Fluxo testavel:

1. subir Kafka manualmente ou aguardar o Docker Compose completo da FASE 8;
2. criar os topicos com `.\infra\kafka\create-topics.ps1`;
3. executar `Inventory.Service`;
4. executar `Order.Service`;
5. criar um pedido com estoque disponivel e observar a mensagem no topico `order-created`.

## FASE 4.1 - Validacao Kafka real

Status: Concluido

Criado:

- `infra/kafka/docker-compose.kafka.yml` para subir apenas Kafka em modo KRaft;
- script `infra/kafka/create-topics.ps1` ajustado para criar `order-created` de forma idempotente;
- documentacao operacional para subir Kafka, criar/listar topicos e consumir `order-created` manualmente.

Decisao didatica:

- esta fase valida o broker Kafka local isolado em `localhost:9092`;
- a FASE 4.1 nao inclui `Order.Service`, `Inventory.Service` ou PostgreSQL no Compose;
- a FASE 8 continua responsavel pelo Docker Compose completo da arquitetura;
- o contrato segue em JSON para interoperabilidade futura entre `Order.Service` .NET e `Payment.Service` Go;
- `OrderId` segue como Kafka message key para preservar ordenacao por pedido dentro de uma partition;
- o problema `SaveChanges` com sucesso e publish Kafka com falha permanece documentado, sem implementar Outbox Pattern.

Validacao:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml config
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
.\infra\kafka\create-topics.ps1
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
```

Fluxo manual para repetir:

1. subir Kafka isolado;
2. criar/listar `order-created`;
3. subir `Inventory.Service`;
4. subir `Order.Service`;
5. cadastrar produto;
6. criar pedido;
7. consumir `order-created` com `kafka-console-consumer.sh` e confirmar o JSON real.

Validacao ponta a ponta executada:

```text
PostgreSQL: acessivel em localhost:5432
Kafka: healthy em localhost:9092
Databases reais: order_db e inventory_db
Produto: 3eb208f9-41fc-42f8-961a-1412b0ec4541 - Notebook Gamer
Pedido: 93c03028-fa50-451c-94d6-def231caf1e3
Kafka topic: order-created
Kafka message key: 93c03028-fa50-451c-94d6-def231caf1e3
Key igual ao OrderId: sim
```

Evento real consumido:

```json
{
  "eventId": "71340ca0-cde8-413a-8833-ecbd9cec7fe3",
  "eventType": "OrderCreated",
  "occurredAt": "2026-09-01T19:31:40.6164926+00:00",
  "correlationId": "5a3f55aeef30c4d7b1929a70f7fbeaca",
  "data": {
    "orderId": "93c03028-fa50-451c-94d6-def231caf1e3",
    "productId": "3eb208f9-41fc-42f8-961a-1412b0ec4541",
    "quantity": 1,
    "unitPrice": 5500.00,
    "totalPrice": 5500.00,
    "status": "Pending",
    "createdAt": "2026-09-01T19:31:40.3505856+00:00"
  }
}
```

## FASE 5 - Payment.Service Go

Status: Concluido

Criado:

- modulo Go em `src/services/Payment.Service`;
- entrypoint `cmd/payment-service`;
- configuracao por variaveis de ambiente para Kafka e limite de aprovacao automatica;
- contrato JSON local em Go para consumir `OrderCreated`;
- consumidor Kafka do topico `order-created` com consumer group `payment-service`;
- simulacao de pagamento com resultado `Approved` ou `Rejected`;
- logs com `eventId`, `orderId`, `productId`, Kafka key, partition e offset;
- testes unitarios para configuracao, contrato e regra de processamento.

Decisao didatica:

- o servico Go nao referencia `Shared.Contracts` .NET;
- a integracao entre .NET e Go acontece pelo JSON publicado no Kafka;
- a FASE 5 consome e processa `OrderCreated`, mas nao publica eventos de pagamento;
- `PaymentApproved` e `PaymentRejected` ficam para a FASE 6;
- `payment_db` e persistencia de pagamentos ainda nao foram implementados.

Validacao:

```powershell
cd .\src\services\Payment.Service
go test ./...
go build .\cmd\payment-service
dotnet build ..\..\..\MicroservicesLab.sln
dotnet test ..\..\..\MicroservicesLab.sln --no-build
```

Validacao integrada executada:

```text
Kafka: healthy em localhost:9092
Topico consumido: order-created
Consumer group: payment-service-fase5-validation-v2
Produto: 3eb208f9-41fc-42f8-961a-1412b0ec4541 - Notebook Gamer
Pedido: a3b3385d-7491-482a-bf18-5cf066a5fd21
Evento: 29f06cdc-5606-4448-b820-a973601ffe8c
Kafka key: a3b3385d-7491-482a-bf18-5cf066a5fd21
Key igual ao OrderId: sim
Resultado simulado: Approved
```

Log real do `Payment.Service`:

```text
received OrderCreated message key=a3b3385d-7491-482a-bf18-5cf066a5fd21 orderId=a3b3385d-7491-482a-bf18-5cf066a5fd21 eventId=29f06cdc-5606-4448-b820-a973601ffe8c partition=1 offset=1
processed OrderCreated payment simulation eventId=29f06cdc-5606-4448-b820-a973601ffe8c orderId=a3b3385d-7491-482a-bf18-5cf066a5fd21 productId=3eb208f9-41fc-42f8-961a-1412b0ec4541 amount=5500 status=Approved reason="amount within automatic approval limit"
```

## FASE 6 - Eventos pagamento

Status: Concluido

Criado:

- contratos `PaymentApprovedData` e `PaymentRejectedData` em `Shared.Contracts`;
- publicacao de `PaymentApproved` e `PaymentRejected` no `Payment.Service` Go;
- configuracao dos topicos `payment-approved` e `payment-rejected`;
- consumer Kafka no `Order.Service` para eventos de pagamento;
- atualizacao de status do pedido para `Paid` ou `PaymentRejected`;
- criacao idempotente dos topicos de pagamento em `infra/kafka/create-topics.ps1`;
- testes unitarios para aplicacao de eventos de pagamento no `Order.Service`.

Decisao didatica:

- `Payment.Service` continua sem banco proprio nesta fase;
- o resultado do pagamento e comunicado por evento, nao por chamada sincrona;
- `Order.Service` nao acessa dados internos de pagamento;
- a Kafka message key dos eventos de pagamento e o `OrderId`;
- consumer groups usados em validacao foram isolados para nao depender de offsets anteriores.

Validacao:

```powershell
.\infra\kafka\create-topics.ps1
cd .\src\services\Payment.Service
go test ./...
go build .\cmd\payment-service
dotnet build ..\..\..\MicroservicesLab.sln
dotnet test ..\..\..\MicroservicesLab.sln --no-build
```

Validacao integrada executada:

```text
Fluxo aprovado:
Pedido: 5ab80059-0f78-422d-bce1-817e505a3beb
OrderCreated: 6e48afbe-09ec-4766-bf9d-bd989d3782a2
PaymentApproved: b7ddb1aa-a82d-4ee8-9476-aaee2f91c2df
Kafka key: 5ab80059-0f78-422d-bce1-817e505a3beb
Status final no order_db: Paid

Fluxo rejeitado:
Pedido: b4eb9ca0-8285-4e19-a897-f8c0ca8e53ca
OrderCreated: 7b92b605-a65d-43eb-b281-e10cd5331968
PaymentRejected: 562eb5f4-2bf0-4b85-9cfb-4fd1bdfdf449
Kafka key: b4eb9ca0-8285-4e19-a897-f8c0ca8e53ca
Status final no order_db: PaymentRejected
```

Evento `PaymentApproved` real:

```json
{
  "eventId": "b7ddb1aa-a82d-4ee8-9476-aaee2f91c2df",
  "eventType": "PaymentApproved",
  "occurredAt": "2026-09-02T03:17:13.9789858Z",
  "correlationId": "99ac73af763b2c1a402709c913e9ef39",
  "data": {
    "paymentId": "49be4829-6f74-4032-ba44-6da784ef2533",
    "orderId": "5ab80059-0f78-422d-bce1-817e505a3beb",
    "amount": 5500,
    "status": "Approved",
    "reason": "amount within automatic approval limit",
    "processedAt": "2026-09-02T03:17:13.9789858Z"
  }
}
```

Evento `PaymentRejected` real:

```json
{
  "eventId": "562eb5f4-2bf0-4b85-9cfb-4fd1bdfdf449",
  "eventType": "PaymentRejected",
  "occurredAt": "2026-09-02T03:17:50.4677155Z",
  "correlationId": "c4267440bc92939d6c5b4b7ac319027e",
  "data": {
    "paymentId": "9a2aa2e1-4296-42ce-b735-9544b19efb76",
    "orderId": "b4eb9ca0-8285-4e19-a897-f8c0ca8e53ca",
    "amount": 11000,
    "status": "Rejected",
    "reason": "amount exceeds automatic approval limit",
    "processedAt": "2026-09-02T03:17:50.4677155Z"
  }
}
```

## FASE 7 - Audit Worker

Status: Concluido

Criado:

- projeto `Audit.Worker` como Worker Service .NET 9;
- `AuditDbContext` com tabela `audit_events` em PostgreSQL;
- migrations do `audit_db`;
- consumidor Kafka para `order-created`, `payment-approved` e `payment-rejected`;
- processamento idempotente por `event_id`;
- armazenamento de `eventType`, topic, message key, partition, offset, correlation id, payload JSON e timestamps;
- testes unitarios para gravacao e ignorar evento duplicado.

Decisao didatica:

- `Audit.Worker` e consumidor assincrono e nao participa da transacao principal de pedido ou pagamento;
- cada envelope Kafka e armazenado como JSON em `payload` para preservar o evento real recebido;
- `event_id` tem indice unico para evitar duplicidade em reprocessamentos;
- as colunas fisicas de Kafka foram nomeadas como `kafka_partition` e `kafka_offset` para evitar conflito com palavras reservadas em consultas PostgreSQL;
- nenhum `audit-events` foi publicado nesta fase: o worker audita os topicos de negocio ja existentes.

Validacao:

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\Payment.Service
go test ./...
go build .\cmd\payment-service
Remove-Item -LiteralPath .\payment-service.exe -ErrorAction SilentlyContinue
```

Validacao integrada executada:

```text
Kafka: healthy em localhost:9092
PostgreSQL: acessivel em localhost:5432
Banco criado para a fase: audit_db
Topicos auditados: order-created, payment-approved, payment-rejected
Consumer group: audit-worker-fase7-validation
Produto: 3eb208f9-41fc-42f8-961a-1412b0ec4541
Pedido: de3bb377-05ed-4e93-a7b8-97ab26747528
Status final do pedido: Paid
Kafka key: de3bb377-05ed-4e93-a7b8-97ab26747528
Key igual ao OrderId: sim
```

Eventos reais registrados no `audit_db` para o pedido:

```text
OrderCreated:
event_id: ec01f81b-beba-4f90-aa20-f7f7c6a64521
topic: order-created
message_key: de3bb377-05ed-4e93-a7b8-97ab26747528
partition: 0
offset: 3

PaymentApproved:
event_id: 4ae0e42c-8cc1-4a82-ad60-a47bd1b902eb
topic: payment-approved
message_key: de3bb377-05ed-4e93-a7b8-97ab26747528
partition: 1
offset: 3
```

Consulta usada para confirmar:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d audit_db -c "SELECT event_id, event_type, topic, message_key, kafka_partition, kafka_offset, occurred_at FROM audit_events WHERE message_key = 'de3bb377-05ed-4e93-a7b8-97ab26747528' ORDER BY audited_at;"
```

## FASE 8 - Docker Compose Completo

Status: Concluido

Criado:

- `docker-compose.yml` principal para subir PostgreSQL, Kafka, inicializacao de topicos, `Inventory.Service`, `Order.Service`, `Payment.Service` e `Audit.Worker`;
- Dockerfiles multi-stage para `Inventory.Service`, `Order.Service`, `Audit.Worker` e `Payment.Service`;
- `.dockerignore`;
- `infra/postgres/init-databases.sql` criando `order_db`, `inventory_db` e `audit_db`;
- configuracao de DNS interno Docker Compose usando `postgres`, `kafka`, `inventory-service`, `order-service`, `payment-service` e `audit-worker`;
- health checks para PostgreSQL, Kafka, `Inventory.Service` e `Order.Service`;
- volumes nomeados `postgres_data` e `kafka_data`.

Decisoes didaticas:

- o Compose principal da raiz e o caminho recomendado a partir da FASE 8;
- o Compose isolado em `infra/kafka/docker-compose.kafka.yml` foi preservado para estudo/debug de Kafka;
- containers usam `kafka:29092` internamente e o host usa `localhost:9092`;
- services usam `postgres:5432` internamente e o host usa `localhost:5432`;
- `Inventory.Service` expoe HTTP em `5276` no host e gRPC em `7242` no host, com gRPC interno em `inventory-service:8081`;
- migrations EF Core sao aplicadas automaticamente somente quando `Database__ApplyMigrations=true`;
- `Payment.Service` continua sem banco proprio nesta fase.

Validacao:

```powershell
docker compose config
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\Payment.Service
go test ./...
docker compose up -d --build
docker compose ps
```

Validacao E2E executada exclusivamente no Compose completo:

```text
Produto: 17aaa602-7840-4b42-abd4-7833e60efbe3 - Produto Compose Fase 8
Pedido: cbe21f22-9290-462f-8656-f8242894b56c
Status inicial: Pending
Status final no order_db: Paid
OrderCreated: ff816e92-9dbe-42e4-bcf0-7daff86ac712
PaymentApproved: d8c3bdd8-00d7-45ea-91c5-d165176befa0
Kafka key: cbe21f22-9290-462f-8656-f8242894b56c
Key igual ao OrderId: sim
Registros no audit_db: 2 para esse pedido
```

Consulta usada para confirmar auditoria:

```powershell
docker compose exec -T postgres psql -U postgres -d audit_db -c "SELECT event_id, event_type, topic, message_key, kafka_partition, kafka_offset, occurred_at FROM audit_events WHERE message_key = 'cbe21f22-9290-462f-8656-f8242894b56c' ORDER BY audited_at;"
```

## FASE 9 - Resiliencia

Status: Concluido

Criado:

- timeout configuravel para a chamada gRPC `Order.Service -> Inventory.Service`;
- retry limitado em `GrpcInventoryClient` para falhas transientes;
- circuit breaker em memoria para falhar rapido quando o Inventory esta repetidamente indisponivel;
- mapeamento de indisponibilidade do Inventory para HTTP `503 Service Unavailable`;
- configuracao `Resilience:Inventory` em `Order.Service` e variaveis equivalentes no Docker Compose;
- teste unitario garantindo que falha do client de Inventory seja classificada como dependencia indisponivel;
- guia operacional `docs/guides/RESILIENCE.md`.

Decisoes didaticas:

- a resiliencia foi aplicada somente ao caminho sincrono `Order.Service -> Inventory.Service`;
- produto inexistente continua sendo regra de negocio e nao abre circuito;
- falhas Kafka foram observadas e documentadas, mas nao corrigidas nesta fase;
- Outbox Pattern, DLQ e observabilidade ficam fora da FASE 9.

Validacao:

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\Payment.Service
go test ./...
docker compose up -d --build
```

Validacao real executada no Compose completo:

```text
Produto usado no lab Inventory offline:
ea15e930-1370-4918-abb1-557e5062bbb4 - Produto Resiliencia Fase 9

Inventory offline:
POST /orders retornou HTTP 503
Circuit breaker abriu por 10 segundos
Pedido nao foi persistido para o produto durante a falha

Recuperacao:
Pedido: d90733cb-1a48-47b9-924b-27191d0030f2
Status final no order_db: Paid
OrderCreated: 0809c2eb-2bc1-4a09-84de-a8f2db86e708
Kafka key: d90733cb-1a48-47b9-924b-27191d0030f2
Key igual ao OrderId: sim

Kafka offline:
Produto: 068bc05b-ad4a-4d4a-8618-dc94d087b4d0 - Produto Kafka Offline Fase 9
Pedido salvo: 68c71987-8076-493d-88db-4aad06b186da
Status: Pending
Cliente: timeout apos 25 segundos
```

## FASE 10 - Observabilidade

Status: Concluido

Criado:

- building block `Observability` para instrumentacao OpenTelemetry dos servicos .NET;
- instrumentacao OpenTelemetry no `Payment.Service` em Go;
- OpenTelemetry Collector no Docker Compose;
- Jaeger para traces distribuidos;
- Prometheus para metricas;
- Grafana com datasource e dashboard provisionados;
- propagacao de contexto distribuido em Kafka por headers W3C `traceparent`/`tracestate`;
- spans de Kafka producer/consumer em `Order.Service`, `Payment.Service` e `Audit.Worker`;
- metricas customizadas para pedidos, pagamentos, chamadas ao Inventory e circuit breaker;
- guia operacional `docs/guides/OBSERVABILITY.md`.

Decisoes didaticas:

- o payload JSON dos eventos nao foi alterado para carregar contexto de trace;
- a correlacao atravessa Kafka por headers, mantendo contratos de dominio limpos;
- a FASE 10 observa o trade-off existente de Kafka sem Outbox, mas nao o resolve;
- alertas, logs centralizados, Kubernetes e Service Mesh ficam para fases futuras.

Validacao:

```powershell
docker compose config
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\Payment.Service
go test ./...
docker compose up -d --build
docker compose ps
```

Validacao real executada no Compose completo:

```text
Jaeger: http://localhost:16686 OK
Prometheus: http://localhost:9090/-/ready OK
Grafana: http://localhost:3000/api/health OK
Dashboard: /d/microserviceslab/microserviceslab provisionado

Fluxo saudavel:
Produto: da168058-7f30-4813-bf9b-132964e929f7 - Produto Observabilidade Fase 10
Pedido: 36eb4ffe-fd94-421c-b7f4-d382d43b2447
TraceId: 07584288df11c4770b3250037e02a984
Status final no order_db: Paid
OrderCreated: topic order-created, key 36eb4ffe-fd94-421c-b7f4-d382d43b2447
PaymentApproved: topic payment-approved, key 36eb4ffe-fd94-421c-b7f4-d382d43b2447

Lab Inventory offline:
Produto: a3874583-49de-47e3-9fb3-7fe5a0a0e402 - Produto Falha Inventory Observabilidade
Tentativas com Inventory offline: HTTP 503
Pedido nao persistido durante a falha
inventory_dependency_failures_total{reason="transient_failure"} = 3
inventory_dependency_failures_total{reason="circuit_open"} = 1
inventory_circuit_breaker_opens_total = 1
orders_rejected_total{reason="inventory_unavailable"} = 4
HTTP 503 em POST /orders = 4

Recuperacao:
Pedido: 3c8354a1-0ae8-437d-a77d-add28452e0c8
TraceId: 9c6aa55a6a6728917044f0d41585a2f5
Status final no order_db: Paid
OrderCreated: topic order-created, partition 1, offset 2
PaymentApproved: topic payment-approved, partition 2, offset 2
```

## FASE 11 - Kubernetes

Status: Concluido

Criado:

- manifests Kubernetes em `k8s/`;
- namespace `microservices-lab`;
- Deployments para `Order.Service`, `Inventory.Service`, `Payment.Service`, `Audit.Worker`, PostgreSQL, Kafka, OpenTelemetry Collector, Jaeger, Prometheus e Grafana;
- Services `ClusterIP` para comunicacao interna;
- ConfigMaps para configuracoes nao sensiveis;
- Secrets para senhas locais de laboratorio;
- PVCs para PostgreSQL e Kafka;
- Job `kafka-init` para criar topicos;
- probes de liveness/readiness para servicos HTTP e infraestrutura;
- scripts simples em `scripts/k8s/`;
- guia operacional `docs/guides/KUBERNETES.md`.

Decisoes didaticas:

- Kubernetes local usado: Docker Desktop Kubernetes;
- Dockerfiles do Compose foram reaproveitados;
- imagens locais foram usadas com `imagePullPolicy: IfNotPresent`;
- acesso pelo host usa `kubectl port-forward`, sem Ingress;
- `Payment.Service` e `Audit.Worker` nao receberam HTTP artificial para health check;
- Kafka e PostgreSQL rodam single-node com PVC local, sem HA;
- Helm, Operators, Service Mesh, HPA e GitOps ficaram fora da fase.

Validacao:

```powershell
kubectl version --client
kubectl cluster-info
kubectl get nodes
docker compose build inventory-service order-service payment-service audit-worker
kubectl apply -k k8s
kubectl get pods -n microservices-lab
kubectl get services -n microservices-lab
```

Validacao real executada no Kubernetes:

```text
Cluster: Docker Desktop Kubernetes
Node: desktop-control-plane
Namespace: microservices-lab

E2E:
Produto: c17b69f1-2066-49d2-b90e-28500ad99a48 - Produto Kubernetes Fase 11
Pedido: 4f3139b6-8297-42fb-b0db-a6b02a6618bc
Status final no order_db: Paid
TraceId: 31b5798a07ededf806c732e1daecc1b8
Audit: OrderCreated e PaymentApproved gravados em audit_db

Kafka:
Topicos: order-created, payment-approved, payment-rejected
Job kafka-init: Completed

Replicas:
inventory-service escalado para 2 replicas

Self-healing:
Pod apagado: inventory-service-f98c66d6f-kwbw7
Novo Pod criado: inventory-service-f98c66d6f-r9sl9
Pedido apos perda de Pod: c821b792-0690-4e64-8c40-4076c6f5220d
Status: Paid

Rolling update:
Imagem nova: microservices-lab-inventory-service:phase11-v2
Deployment: inventory-service
Resultado: rollout concluido com sucesso
Pedido pos-rollout: 36ca1331-3790-490d-9090-558d7a212262
Status: Paid

Observabilidade:
Jaeger trace: 31b5798a07ededf806c732e1daecc1b8
Prometheus: orders_created_total e payments_approved_total coletados
Grafana: dashboard /d/microserviceslab/microserviceslab provisionado
```

## FASE 12 - Labs, documentacao e fechamento

Status: Concluido

Criado:

- indice de laboratorios em `docs/labs/README.md`;
- labs praticos para happy path, estoque insuficiente, resiliencia, Kafka, observabilidade, self-healing, scaling e troubleshooting Kubernetes/Kafka;
- guia rapido em `docs/guides/QUICK-REFERENCE.md`;
- guia de troubleshooting em `docs/guides/TROUBLESHOOTING.md`;
- ajustes no `README.md`, `docs/ROADMAP.md`, `docs/ARCHITECTURE.md` e `docs/guides/RUNNING-LOCALLY.md`.

Decisoes didaticas:

- a FASE 12 nao adicionou novos servicos nem alterou a arquitetura;
- Docker Compose e Kubernetes foram preservados como modos validos de execucao;
- os labs usam comandos PowerShell/Windows e caminhos reais do repositorio;
- `price-updated` permanecia planejado para a Parte II naquele fechamento.

Validacao:

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\Payment.Service
go test ./...
docker compose config --quiet
kubectl apply -k k8s --dry-run=client
```

Resultado da validacao de fechamento:

```text
dotnet build: aprovado, 0 avisos, 0 erros
dotnet test --no-build: aprovado, 16/16 testes
go test ./...: aprovado
docker compose config --quiet: aprovado
kubectl apply -k k8s --dry-run=client: aprovado
links Markdown: 24 arquivos verificados, sem links internos quebrados
```

Smoke test Kubernetes executado:

```text
Namespace: microservices-lab
Produto: 93ac3382-5bee-4a91-bb1c-a6b410ac3f17 - Produto Fase 12 Smoke 2
Pedido: 66c3f496-6377-4dec-9c4f-e02bbb97542a
Status inicial: Pending
Status final: Paid
OrderCreated: 157521f5-99b1-4634-bee0-c7af89a5f23f
PaymentApproved: d6c81b46-54f8-4366-9873-a9915669378e
Kafka key: 66c3f496-6377-4dec-9c4f-e02bbb97542a
Audit: OrderCreated e PaymentApproved gravados em audit_db
```

Correcao feita durante a validacao:

- alinhadas referencias de pacotes dos testes para `9.*`, removendo conflito entre `Microsoft.EntityFrameworkCore.Relational` 9.0.19 e 9.0.20;
- nenhum servico, manifesto, topico ou contrato de runtime foi alterado.

## FASE 13 - PriceFeed.Service Go

Status: Concluido

Criado:

- `PriceFeed.Service` em Go em `src/services/PriceFeed.Service`;
- `MockPriceProvider` com estado por simbolo, precos iniciais, pequenas oscilacoes e intervalo configuravel;
- `PriceFeed` desacoplado de provider concreto e de destino concreto;
- `ConsolePublisher` para observar o fluxo localmente sem Kafka;
- configuracao por variaveis de ambiente para provider, simbolos, precos iniciais, intervalo e variacao maxima;
- Dockerfile do servico, sem integracao ao Docker Compose nesta fase;
- testes unitarios para provider, feed, publisher, configuracao e representacao de preco.

Decisoes didaticas:

- a fonte inicial de precos e simulada, mas fica atras da abstracao `PriceProvider`;
- Kafka, SSE, WebSocket, `Pricing.Service`, backpressure e load testing permanecem para fases futuras;
- os precos sao representados em centavos (`int64`), evitando arredondamento basico de `float64` sem adicionar biblioteca decimal externa;
- o fluxo usa goroutines por simbolo, channel unico de saida e cancelamento por `context.Context`.

Validacao:

```powershell
cd .\src\services\PriceFeed.Service
go test ./...
go test -race ./...
go run .
$env:PRICE_UPDATE_INTERVAL = "100ms"
go run .
cd ..\Payment.Service
go test ./...
cd ..\..\..
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
```

Resultado da validacao:

- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test -race ./...` em `PriceFeed.Service`: tentado, mas o toolchain local exigiu `CGO_ENABLED=1` e depois falhou ao carregar `runtime/race`;
- `go test ./...` em `Payment.Service`: aprovado;
- `dotnet build .\MicroservicesLab.sln`: aprovado;
- `dotnet test .\MicroservicesLab.sln --no-build`: aprovado, 16/16 testes.

## FASE 14 - Realtime Delivery - SSE vs WebSocket

Status: Concluido

Criado:

- servidor HTTP no `PriceFeed.Service`;
- endpoint `GET /health`;
- endpoint SSE `GET /prices/stream`;
- `Broadcaster` para distribuir cada `PriceUpdate` para multiplos subscribers;
- adaptador HTTP/SSE separado do nucleo do `PriceFeed`;
- testes para SSE, headers, broadcasting, desconexao, health e politica de subscriber lento.

Decisoes didaticas:

- SSE foi escolhido porque o fluxo atual e unidirecional, do servidor para clientes;
- WebSocket nao foi implementado porque ainda nao ha requisito de comunicacao bidirecional continua;
- `ConsolePublisher` foi preservado e combinado com o broadcaster por um `MultiPublisher`;
- clientes lentos possuem channel bufferizado e, quando o buffer fica cheio, perdem novas atualizacoes em vez de bloquear todos os demais clientes;
- Kafka, gRPC streaming, `Pricing.Service`, backpressure avancado e load testing permanecem para fases futuras.

Validacao:

```powershell
cd .\src\services\PriceFeed.Service
go test ./...
$env:CGO_ENABLED = "1"
go test -race ./...
go run .
curl http://localhost:8080/health
curl -N http://localhost:8080/prices/stream
cd ..\Payment.Service
go test ./...
cd ..\..\..
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
```

Resultado da validacao:

- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test -race ./...` com `CGO_ENABLED=1`: tentado, mas bloqueado porque `gcc` nao esta disponivel no `PATH`;
- mini-lab `/health`: aprovado;
- mini-lab SSE com 1 cliente: aprovado;
- mini-lab SSE com 2 clientes: aprovado, ambos receberam a mesma sequencia de eventos;
- desconexao de clientes por encerramento do `curl --max-time`: aprovada, o servico continuou executando;
- shutdown por `Ctrl+C`: implementado no codigo e coberto por contexto, mas o PTY do ambiente nao propagou o sinal ao processo Go durante o mini-lab;
- `go test ./...` em `Payment.Service`: aprovado;
- `dotnet build .\MicroservicesLab.sln`: aprovado;
- `dotnet test .\MicroservicesLab.sln --no-build`: aprovado, 16/16 testes.

## Mini-lab Realtime - gRPC Server Streaming

Status: Concluido

Criado:

- contrato `src/protos/pricefeed.proto`;
- adaptador gRPC Server Streaming no `PriceFeed.Service`;
- configuracao `PRICE_GRPC_ADDR`, com padrao `:9090`;
- `Pricing.Service` .NET como cliente console didatico;
- lab `docs/labs/LAB-RT-GRPC-SERVER-STREAMING.md`;
- testes para streaming gRPC e cancelamento do subscriber.

Decisoes didaticas:

- o nucleo do `PriceFeed` continua desacoplado de gRPC, SSE, .NET e Kafka;
- gRPC assina o mesmo `Broadcaster` usado pelo SSE;
- `Pricing.Service` nao recebeu banco, REST, Clean Architecture, MediatR ou consumo Kafka nesta etapa;
- Kafka e o evento `PriceUpdated` permanecem para a FASE 15;
- posteriormente, a FASE 16 evoluiu `Pricing.Service` como consumidor Kafka com estado derivado.

Validacao:

```powershell
cd .\src\services\PriceFeed.Service
go test ./...
$env:CGO_ENABLED = "1"
go test -race ./...
$env:PRICE_UPDATE_INTERVAL = "100ms"
$env:PRICE_GRPC_ADDR = ":19090"
$env:PRICE_HTTP_ADDR = ":18080"
go run .
cd ..\Pricing.Service
$env:PRICE_FEED_GRPC_ADDRESS = "http://localhost:19090"
dotnet run --no-build
cd ..\Payment.Service
go test ./...
cd ..\..\..
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
```

Resultado da validacao:

- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test -race ./...` com `CGO_ENABLED=1`: tentado, mas bloqueado porque `gcc` nao esta disponivel no `PATH`;
- smoke test manual gRPC: aprovado, `Pricing.Service` recebeu fluxo real de `PETR4`, `VALE3`, `ITUB4`, `WEGE3` e `PRIO3`;
- cancelamento do subscriber gRPC: coberto por teste automatizado; no smoke test, o `Ctrl+C` do PTY encerrou o runner local sem servir como evidencia adicional de graceful shutdown;
- `go test ./...` em `Payment.Service`: aprovado;
- `dotnet build .\MicroservicesLab.sln`: aprovado;
- `dotnet test .\MicroservicesLab.sln --no-build`: aprovado, 16/16 testes.

## FASE 15 - Kafka PriceUpdated Stream

Status: Concluido

Criado:

- `KafkaPublisher` no `PriceFeed.Service`;
- evento JSON `PriceUpdated` com `symbol`, `price` em centavos e `occurredAt`;
- publicacao no topico `price-updated`;
- Kafka message key igual ao `symbol`;
- configuracao `PRICE_KAFKA_ENABLED`, `PRICE_KAFKA_BOOTSTRAP_SERVERS` e `PRICE_KAFKA_TOPIC`;
- topico `price-updated` com 3 partitions e replication factor 1 no `docker-compose.yml` e em `infra/kafka/create-topics.ps1`;
- `pricefeed-service` no Docker Compose com Kafka habilitado;
- lab `docs/labs/LAB-15-kafka-price-updated.md`;
- testes unitarios para serializacao, key, configuracao e erro de escrita Kafka.

Decisoes didaticas:

- Kafka entra como adapter de `Publisher`; o nucleo do `PriceFeed` continua sem conhecer Kafka;
- Kafka fica desabilitado por padrao para preservar `go run .` sem broker;
- o evento nao usa envelope completo nesta fase, mantendo foco em producer, topic, key, partitions e stream;
- `symbol` e a key para observar particionamento e ordering por ativo;
- falha de escrita Kafka e registrada em log e nao derruba console, SSE ou gRPC;
- retry sofisticado, outbox, DLQ, exactly-once e consumer de aplicacao ficam fora do escopo;
- `Pricing.Service` nao foi alterado para consumir Kafka.

Validacao:

```powershell
cd .\src\services\PriceFeed.Service
go test ./...
$env:CGO_ENABLED = "1"
go test -race ./...
cd ..\Payment.Service
go test ./...
cd ..\..\..
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
```

Resultado da validacao:

- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test -race ./...` com `CGO_ENABLED=1`: tentado, mas bloqueado porque `gcc` nao esta disponivel no `PATH`;
- `go test ./...` em `Payment.Service`: aprovado;
- `dotnet build .\MicroservicesLab.sln`: aprovado;
- `dotnet test .\MicroservicesLab.sln --no-build`: aprovado, 16/16 testes;
- `docker compose config --quiet`: aprovado;
- Kafka isolado subiu com `docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d`;
- `price-updated` criado/listado com sucesso;
- `price-updated`: 3 partitions, replication factor 1;
- smoke test real: `PriceFeed.Service` publicou eventos reais no Kafka com `PRICE_KAFKA_ENABLED=true`, usando o `KafkaPublisher` assíncrono;
- consumer CLI recebeu mensagens reais com key `PRIO3`, `ITUB4`, `WEGE3`, `PETR4` e `VALE3`;
- consumer CLI com metadados mostrou `Partition:0 | Offset:0 | PRIO3 | {"symbol":"PRIO3","price":4835,...}`.

## FASE 16 - Pricing.Service .NET

Status: Concluido

Criado/evoluido:

- `Pricing.Service` deixou de ser apenas console gRPC e passou a ser uma minimal API .NET;
- `KafkaPriceUpdatedConsumer` consumindo o topico `price-updated`;
- consumer group `pricing-service`;
- contrato .NET `PriceUpdatedEvent` com `Symbol`, `Price` em centavos e `OccurredAt`;
- validacao minima do JSON recebido;
- aviso em log quando Kafka key e `symbol` divergem;
- `LatestPriceStore` thread-safe em memoria para ultimo preco conhecido por simbolo;
- endpoints `GET /health`, `GET /prices` e `GET /prices/{symbol}`;
- modo opcional `GrpcLab:Enabled=true` preservando o mini-lab de gRPC Server Streaming;
- `pricing-service` no Docker Compose, publicado em `http://localhost:5090`;
- testes automatizados para store, parser e endpoints;
- lab `docs/labs/LAB-16-pricing-service-kafka.md`.

Decisoes didaticas:

- Kafka e o caminho principal da FASE 16 para alimentar o estado derivado;
- gRPC continua como comunicacao direta de laboratorio, mas nao alimenta o estado REST;
- `ConcurrentDictionary` foi usado porque ha escrita pelo consumer Kafka e leituras simultaneas via HTTP;
- nao foram adicionados banco, Redis, CQRS, Event Sourcing, DLQ, retry sofisticado ou backpressure;
- `AutoOffsetReset=Earliest` permite que um consumer group novo leia desde o inicio do log retido; grupos existentes retomam dos offsets commitados.

Validacao:

```powershell
dotnet test .\tests\Pricing.Service.Tests\Pricing.Service.Tests.csproj
dotnet build .\src\services\Pricing.Service\Pricing.Service.csproj
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln
cd .\src\services\PriceFeed.Service
go test ./...
cd ..\Payment.Service
go test ./...
docker compose config --quiet
```

Resultado da validacao:

- `Pricing.Service.Tests`: aprovado, 11/11 testes;
- build isolado do `Pricing.Service`: aprovado;
- `dotnet build .\MicroservicesLab.sln`: aprovado, 0 warnings, 0 errors;
- `dotnet test .\MicroservicesLab.sln`: aprovado, 27/27 testes;
- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test ./...` em `Payment.Service`: aprovado;
- `docker compose config --quiet`: aprovado;
- smoke real: Kafka existente em `localhost:9092` estava saudavel no container `microservices-lab-kafka-1`;
- topico `price-updated`: 3 partitions, replication factor 1;
- `PriceFeed.Service` publicou eventos reais com `PRICE_KAFKA_ENABLED=true`;
- `Pricing.Service` consumiu `price-updated` com o consumer group `pricing-service-smoke-fase16`;
- `GET /health` retornou `Healthy`;
- `GET /prices` retornou estado derivado real, incluindo `PRIO3`;
- `GET /prices/PRIO3` retornou `{"symbol":"PRIO3","price":5279,"occurredAt":"2026-10-01T10:33:27.9110091-03:00"}`;
- interrupcao e reinicio do `Pricing.Service` foram testados mantendo `PriceFeed.Service` produzindo; apos reiniciar, o consumer group voltou a consumir e a API exibiu estado derivado novamente.

## FASE 23 - Angular, estrutura inicial e Docker Compose

Status: Concluido

Criado:

- aplicacao Angular em `src/frontend/MicroservicesLab.App`;
- componentes standalone com Angular Router;
- navegacao inicial para dashboard, produtos, pedidos, precos, auditoria e observabilidade;
- pagina placeholder simples informando que o laboratorio esta disponivel;
- configuracao central de endpoints em `src/environments`;
- `proxy.conf.json` para desenvolvimento local sem CORS;
- `Dockerfile` multi-stage usando build Node e Nginx;
- `nginx.conf` com fallback de SPA e proxy para os servicos internos do Compose;
- servico `frontend` no `docker-compose.yml`, publicado em `http://localhost:4200`.

Decisoes didaticas:

- a FASE 23 prepara a base da aplicacao sem implementar funcionalidades das telas futuras;
- nao foi criado API Gateway ou BFF;
- as rotas `/api/order`, `/api/inventory`, `/api/pricefeed` e `/api/pricing` isolam o front-end das portas reais dos servicos;
- no desenvolvimento local, o proxy do Angular aponta para `localhost:5074`, `localhost:5276`, `localhost:5080` e `localhost:5090`;
- no Docker, o Nginx encaminha para `order-service:8080`, `inventory-service:8080`, `pricefeed-service:8080` e `pricing-service:8080`.

Validacao:

```powershell
node --version
pnpm --version
pnpm install --frozen-lockfile
pnpm build
docker compose config --quiet
docker compose build frontend
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln
cd .\src\services\PriceFeed.Service
go test ./...
cd ..\Payment.Service
go test ./...
```

Resultado da validacao:

- `dotnet test .\tests\Audit.Worker.Tests\Audit.Worker.Tests.csproj`: aprovado, 7/7 testes;
- build Angular local: aprovado com `pnpm build`;
- `docker compose config --quiet`: aprovado;
- `docker compose up -d --build`: aprovado, incluindo `audit-worker` com HTTP em `http://localhost:5288`;
- `dotnet build .\MicroservicesLab.sln`: aprovado, 0 warnings, 0 errors;
- `dotnet test .\MicroservicesLab.sln --no-build`: aprovado, 32/32 testes;
- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test ./...` em `Payment.Service`: aprovado;
- smoke via Docker/Nginx: `GET http://127.0.0.1:4200/auditoria` retornou HTTP 200;
- `GET http://127.0.0.1:4200/api/audit/health` retornou HTTP 200;
- pedido real criado para validacao: `88dc3e3f-a4df-4945-8b3b-851e43f0813e`;
- pagamento confirmado com transicao `Pending -> Paid`;
- consulta `GET /api/audit/audit-events?messageKey=88dc3e3f-a4df-4945-8b3b-851e43f0813e&limit=20` retornou 2 eventos reais: `PaymentApproved` e `OrderCreated`;
- filtro por tipo `eventType=OrderCreated` retornou 1 evento;
- validacao de duplicidade confirmou que nao houve `EventId` duplicado persistido para o pedido;
- parametro invalido `limit=999` retornou HTTP 400 com mensagem `limit must be between 1 and 200.`.

## FASE 28.1 - API de consulta de auditoria e interface Angular

Status: Concluido

Arquitetura escolhida:

- a API HTTP foi hospedada no proprio `Audit.Worker`;
- o consumidor Kafka existente continua sendo o unico consumidor de auditoria;
- o mesmo `AuditDbContext` consulta `audit_db`;
- nao foi criado servico separado, API Gateway ou BFF.

Criado/evoluido:

- endpoint `GET /audit-events` no `Audit.Worker`;
- endpoint `GET /health` no `Audit.Worker`;
- consulta read-only com `AsNoTracking()`;
- ordenacao do mais recente para o mais antigo;
- limite de resultados com maximo de 200 registros;
- filtros opcionais por `eventType` e `messageKey`;
- alias `orderId` aceito quando equivalente a `messageKey`;
- proxy Docker/Nginx e proxy de desenvolvimento em `/api/audit`;
- tela Angular funcional na rota `/auditoria`;
- listagem de eventos persistidos;
- detalhes do evento selecionado;
- payload completo preservado e formatado quando JSON valido.

Decisoes didaticas:

- o schema do banco nao foi alterado;
- nenhuma migration foi criada;
- a idempotencia existente por `EventId` foi preservada;
- contratos Kafka e regras de pedidos/pagamentos nao foram modificados;
- nao foi implementado polling automatico, edicao, exclusao ou busca textual complexa.

Validacao:

```powershell
dotnet test .\tests\Audit.Worker.Tests\Audit.Worker.Tests.csproj
cd .\src\frontend\MicroservicesLab.App
pnpm build
docker compose config --quiet
docker compose up -d --build
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\PriceFeed.Service
go test ./...
cd ..\Payment.Service
go test ./...
```

Resultado da validacao:

- build Angular local: aprovado com `pnpm build`;
- `docker compose config --quiet`: aprovado;
- `docker compose up -d --build frontend`: aprovado, imagem `microservices-lab-frontend:latest` reconstruida e container iniciado;
- `dotnet build .\MicroservicesLab.sln`: aprovado, 0 warnings, 0 errors;
- `dotnet test .\MicroservicesLab.sln --no-build`: aprovado, 27/27 testes;
- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test ./...` em `Payment.Service`: aprovado;
- smoke via Docker/Nginx: `GET http://127.0.0.1:4200/precos` retornou HTTP 200;
- snapshot REST via `GET http://127.0.0.1:4200/api/pricing/prices` retornou 5 simbolos;
- stream SSE via `GET http://127.0.0.1:4200/api/pricefeed/prices/stream` retornou eventos reais `price-update`;
- em uma janela de 6 segundos, foram observados 60 eventos SSE;
- exemplo real recebido: `{"symbol":"WEGE3","price":4030,"occurredAt":"2026-10-11T04:18:56.943107183Z"}`;
- falhas forcadas de servicos nao foram executadas nesta validacao para evitar interromper o ambiente local; a tela mantem REST e SSE em secoes independentes e trata erro por fluxo.

## FASE 27 - Precos realtime (SSE)

Status: Concluido

Criado/evoluido:

- tela funcional na rota `/precos`;
- snapshot REST consumindo `GET /api/pricing/prices`;
- stream realtime consumindo `GET /api/pricefeed/prices/stream`;
- servico Angular especifico para SSE usando `EventSource`;
- tratamento do evento SSE real `price-update`;
- tipagem do payload real `{ symbol, price, occurredAt }`;
- separacao visual entre REST/Kafka e SSE direto;
- estados de conexao: conectando, conectado, reconectando, desconectado e erro;
- destaque visual breve quando um simbolo recebe novo preco;
- historico recente de eventos SSE recebidos;
- encerramento da conexao SSE ao sair da tela.

Decisoes didaticas:

- o snapshot REST representa o estado processado pelo `Pricing.Service` apos consumo Kafka;
- o stream SSE representa atualizacoes diretas do `PriceFeed.Service`;
- os dois fluxos sao exibidos separadamente porque podem divergir momentaneamente;
- nao foram implementados WebSocket, graficos complexos, API Gateway, BFF ou novos endpoints.

Validacao:

```powershell
cd .\src\frontend\MicroservicesLab.App
pnpm build
docker compose config --quiet
docker compose up -d --build frontend
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\PriceFeed.Service
go test ./...
cd ..\Payment.Service
go test ./...
```

Resultado da validacao:

- build Angular local: aprovado com `pnpm build`;
- `docker compose config --quiet`: aprovado;
- `docker compose up -d --build frontend`: aprovado, imagem `microservices-lab-frontend:latest` reconstruida e container iniciado;
- `dotnet build .\MicroservicesLab.sln`: aprovado, 0 warnings, 0 errors;
- `dotnet test .\MicroservicesLab.sln --no-build`: aprovado, 27/27 testes;
- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test ./...` em `Payment.Service`: aprovado;
- smoke via Docker/Nginx: `GET http://127.0.0.1:4200/pedidos` retornou HTTP 200;
- pedido real criado via proxy do front-end: `5b6ec07b-17a9-489e-be2a-d05793153ce2`, produto `f525ba73-8eb8-4d6f-aaab-a37546146daa`, quantidade `1`, total `5500.00`;
- polling manual confirmou transicao `Pending -> Paid`;
- durante a primeira tentativa de smoke, o `Payment.Service` nao estava em execucao por falha anterior de conexao ao Kafka; o servico foi iniciado com `docker compose up -d payment-service` e a validacao foi repetida com sucesso.

Resultado da validacao:

- `node --version`: disponivel (`v24.19.0`);
- `pnpm --version`: aprovado (`11.25.0`);
- `pnpm install --frozen-lockfile`: aprovado;
- build Angular local: aprovado com `pnpm build`;
- smoke local: `http://127.0.0.1:4200/` e `http://127.0.0.1:4200/produtos` retornaram HTTP 200;
- `docker compose config --quiet`: aprovado;
- `docker compose build frontend`: aprovado, imagem `microservices-lab-frontend:latest` criada;
- `dotnet build .\MicroservicesLab.sln`: aprovado, 0 warnings, 0 errors;
- `dotnet test .\MicroservicesLab.sln`: aprovado, 27/27 testes;
- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test ./...` em `Payment.Service`: aprovado.

## FASE 24 - Dashboard e navegacao

Status: Concluido

Criado/evoluido:

- dashboard funcional na rota `/dashboard`;
- cards com total de produtos, pedidos e simbolos com preco;
- consumo real de `GET /api/inventory/products`, `GET /api/order/orders` e `GET /api/pricing/prices`;
- status dos servicos HTTP acessiveis pelo front-end usando endpoints `/health`;
- estados independentes de carregamento, erro, indisponibilidade e vazio;
- botao manual para atualizar indicadores;
- servicos Angular especificos para Inventory, Order e Pricing;
- navegacao da FASE 23 preservada, com destaque de rota ativa;
- paginas de produtos, pedidos, precos, auditoria e observabilidade preservadas como placeholders.

Decisoes didaticas:

- nao foi implementado polling automatico;
- nao foi implementado SSE nesta fase;
- nao foram criados API Gateway, BFF, autenticacao ou novos endpoints backend;
- quando uma API falha, o dashboard exibe `Indisponivel` e mantem os demais indicadores funcionando.

Validacao:

```powershell
cd .\src\frontend\MicroservicesLab.App
pnpm build
pnpm start
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:4200/
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:4200/produtos
docker compose config --quiet
docker compose up -d --build
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\PriceFeed.Service
go test ./...
cd ..\Payment.Service
go test ./...
```

## FASE 25 - Produtos e estoque

Status: Concluido

Criado/evoluido:

- tela funcional na rota `/produtos`;
- listagem real de produtos consumindo `GET /api/inventory/products`;
- cadastro de produtos consumindo `POST /api/inventory/products`;
- painel de detalhes consumindo `GET /api/inventory/products/{id}`;
- consulta de disponibilidade consumindo `POST /api/inventory/products/{id}/availability`;
- formularios reativos com validacao de nome, preco e quantidade;
- estados de loading, vazio, sucesso e erro;
- atualizacao manual da listagem;
- layout responsivo integrado a identidade visual da Parte III;
- suporte ao locale `pt-BR` para exibicao monetaria em BRL.

Decisoes didaticas:

- a tela consome somente APIs existentes do `Inventory.Service`;
- nao foram implementadas edicao, exclusao ou reserva manual de estoque;
- a reserva de estoque continua reservada para os fluxos de pedidos das proximas fases;
- nao foram criados API Gateway, BFF, autenticacao ou novos endpoints backend.

Validacao:

```powershell
cd .\src\frontend\MicroservicesLab.App
pnpm build
docker compose config --quiet
docker compose up -d --build frontend
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\PriceFeed.Service
go test ./...
cd ..\Payment.Service
go test ./...
```

Resultado da validacao:

- build Angular local: aprovado com `pnpm build`;
- `docker compose config --quiet`: aprovado;
- `docker compose up -d --build frontend`: aprovado, imagem `microservices-lab-frontend:latest` reconstruida e container iniciado;
- `dotnet build .\MicroservicesLab.sln`: aprovado, 0 warnings, 0 errors;
- `dotnet test .\MicroservicesLab.sln --no-build`: aprovado, 27/27 testes;
- `go test ./...` em `PriceFeed.Service`: aprovado;
- `go test ./...` em `Payment.Service`: aprovado;
- smoke via Docker/Nginx: `GET http://127.0.0.1:4200/produtos` retornou HTTP 200;
- produto real criado via proxy do front-end: `7147eab7-a183-489c-8b46-934e9bbe4ad0`, `Produto Frontend Fase 25`, preco `123.45`, estoque `7`;
- detalhes do produto consultados com sucesso;
- disponibilidade para quantidade `3` retornou `isAvailable=true` e preservou `availableQuantity=7`.

## FASE 26.1 - Analise da integracao entre pedidos e estoque

Status: Concluido sem implementacao

Conclusao:

- o contrato gRPC ja possui `ReserveStock`;
- o `Inventory.Service` ja implementa reserva via HTTP e gRPC;
- o `Order.Service` atualmente usa somente `CheckAvailability`;
- a reserva atual nao possui idempotencia nem protecao explicita contra concorrencia;
- a integracao segura da reserva foi adiada para a FASE 29.1.

Pendencia futura da FASE 29.1:

- reserva atomica;
- idempotencia;
- compensacao de estoque;
- concorrencia;
- tratamento de falhas distribuidas.

## FASE 26.2 - Interface de pedidos e acompanhamento de pagamentos

Status: Concluido

Criado/evoluido:

- tela funcional na rota `/pedidos`;
- listagem real de pedidos consumindo `GET /api/order/orders`;
- criacao de pedidos consumindo `POST /api/order/orders`;
- consulta de detalhes consumindo `GET /api/order/orders/{id}`;
- seletor de produtos reais carregados do `Inventory.Service`;
- formularios reativos com validacao de produto e quantidade;
- status visuais para `Pending`, `Paid` e `PaymentRejected`;
- polling de detalhes enquanto o pedido esta `Pending`;
- interrupcao de polling em status final, falhas consecutivas ou destruicao do componente;
- estados de loading, vazio, sucesso e erro;
- atualizacao manual da lista e do pedido selecionado.

Decisoes didaticas:

- nao foi criado endpoint HTTP no `Payment.Service`;
- nao foram implementados SSE ou WebSocket para pedidos;
- nao foi implementada reserva automatica de estoque;
- a tela usa exclusivamente o comportamento atual do backend.

Validacao:

```powershell
cd .\src\frontend\MicroservicesLab.App
pnpm build
docker compose config --quiet
docker compose up -d --build frontend
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
cd .\src\services\PriceFeed.Service
go test ./...
cd ..\Payment.Service
go test ./...
```
