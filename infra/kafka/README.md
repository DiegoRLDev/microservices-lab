# Kafka no MicroservicesLab

Esta pasta contem a infraestrutura isolada de Kafka para a FASE 4.1.

A FASE 4.1 existe para validar, de forma pratica, que o `Order.Service` consegue publicar o evento real `OrderCreated` em um broker Kafka local.

Ela nao substitui a FASE 8. A diferenca e:

- FASE 4.1: sobe somente Kafka para validacao e aprendizado.
- FASE 8: sobe toda a arquitetura via Docker Compose, incluindo servicos, PostgreSQL, Kafka e workers.

## Subir Kafka

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
```

Confirmar container:

```powershell
docker ps --filter "name=kafka"
```

Verificar health:

```powershell
docker inspect kafka --format "{{json .State.Health}}"
```

Parar Kafka:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml down
```

Limpar volume local:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml down -v
```

## Topicos Kafka

Na FASE 4.1, o primeiro topico validado foi:

```text
order-created
```

Na FASE 6, o script tambem cria os topicos de pagamento:

```text
payment-approved
payment-rejected
```

Na FASE 7, `Audit.Worker` consome `order-created`, `payment-approved` e `payment-rejected`. O topico `audit-events` nao esta ativo nesta fase.

## Criar Topicos

```powershell
.\infra\kafka\create-topics.ps1
```

O script usa `--if-not-exists`, entao pode ser executado mais de uma vez.

Se o PowerShell bloquear scripts temporariamente:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\infra\kafka\create-topics.ps1
```

Isso altera a policy apenas no processo atual.

Alternativa sem executar script PowerShell:

```powershell
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --create --if-not-exists --topic order-created --partitions 3 --replication-factor 1
```

## Listar Topicos

```powershell
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list
```

## Consumir Manualmente

Para visualizar o JSON real publicado pelo `Order.Service`:

```powershell
docker exec -it kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic order-created --from-beginning --property print.key=true --property key.separator=" | "
```

A chave impressa antes do separador deve ser o `OrderId`.

## Producer do Order.Service

Configuracao esperada:

- bootstrap servers: `localhost:9092`;
- topic: `order-created`;
- key: `OrderId`;
- serializacao: JSON;
- `acks=all`;
- idempotencia habilitada no producer Kafka.

Usar `OrderId` como key ajuda a preservar a ordenacao de eventos relacionados ao mesmo pedido dentro de uma partition. Isso nao cria ordenacao global entre todos os pedidos.

## Fluxo de Validacao

1. Subir Kafka.
2. Criar/listar o topico `order-created`.
3. Subir `Inventory.Service`.
4. Subir `Order.Service`.
5. Criar um produto com estoque disponivel.
6. Criar um pedido com `POST /orders`.
7. Confirmar que o pedido foi salvo no `OrdersDb`.
8. Consumir manualmente `order-created` e observar o JSON real.

Fluxo:

```text
POST /orders
     |
     v
Order.Service
     |
     | gRPC
     v
Inventory.Service
     |
     | estoque disponivel
     v
Order.Service
     |
     | SaveChanges
     v
OrdersDb
     |
     | OrderCreated
     v
Kafka
     |
     v
order-created
     |
     v
consumer manual
```

## Exemplo de Evento

O contrato real inclui `status` e `createdAt` dentro de `data`, alem dos campos essenciais do pedido:

```json
{
  "eventId": "uuid",
  "eventType": "OrderCreated",
  "occurredAt": "timestamp",
  "correlationId": "trace-id-or-uuid",
  "data": {
    "orderId": "uuid",
    "productId": "uuid",
    "quantity": 2,
    "unitPrice": 100.00,
    "totalPrice": 200.00,
    "status": "Pending",
    "createdAt": "timestamp"
  }
}
```

O JSON e independente de linguagem para que, na FASE 5, o `Payment.Service` em Go consiga consumir o evento sem depender de assemblies .NET.

## Validacao Real Executada

Resultado da validacao ponta a ponta da FASE 4.1:

```text
PostgreSQL: acessivel em localhost:5432
Kafka: healthy em localhost:9092
Topic: order-created
Produto: 3eb208f9-41fc-42f8-961a-1412b0ec4541 - Notebook Gamer
Pedido: 93c03028-fa50-451c-94d6-def231caf1e3
Kafka message key: 93c03028-fa50-451c-94d6-def231caf1e3
Key igual ao OrderId: sim
```

Evento observado no consumer manual:

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

## Cenario de Erro Didatico

A arquitetura atual ainda permite este problema:

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

Isso acontece porque PostgreSQL e Kafka nao participam da mesma transacao. O Outbox Pattern e uma solucao comum para esse tipo de inconsistencia, mas ele nao sera implementado nesta etapa. Manter o problema visivel ajuda a estudar o trade-off antes de adicionar a solucao.
