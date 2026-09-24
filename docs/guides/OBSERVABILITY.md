# Guia de Observabilidade

Este guia registra a FASE 10 do MicroservicesLab. O foco desta fase e tornar observavel o fluxo completo:

```text
POST /orders
  -> Order.Service
  -> gRPC Inventory.Service
  -> order_db
  -> Kafka order-created
  -> Payment.Service
  -> Kafka payment-approved/payment-rejected
  -> Order.Service
  -> Audit.Worker
  -> audit_db
```

## Escopo da FASE 10

Implementado:

- OpenTelemetry em `Order.Service`, `Inventory.Service`, `Payment.Service` e `Audit.Worker`;
- traces distribuidos via OTLP para Jaeger;
- metricas via OTLP para OpenTelemetry Collector e Prometheus;
- dashboard Grafana provisionado automaticamente;
- correlacao de logs com `TraceId` e `SpanId`;
- propagacao de contexto entre .NET, Kafka, Go, Kafka e .NET usando headers W3C `traceparent`/`tracestate`;
- spans para HTTP, gRPC, EF Core, Kafka produce/consume e processamento de pagamento;
- metricas customizadas de pedidos, pagamentos, chamadas ao Inventory e circuit breaker.

Fora do escopo desta fase:

- alertas;
- logs centralizados;
- Service Mesh;
- Outbox Pattern;
- DLQ;
- Kubernetes.

## Endpoints Locais

Com o Docker Compose completo rodando:

| Componente | URL |
| --- | --- |
| Order.Service | `http://localhost:5074` |
| Inventory.Service | `http://localhost:5276` |
| Inventory gRPC no host | `localhost:7242` |
| Jaeger | `http://localhost:16686` |
| Prometheus | `http://localhost:9090` |
| Grafana | `http://localhost:3000` |
| Dashboard Grafana | `http://localhost:3000/d/microserviceslab/microserviceslab` |
| OTLP gRPC | `localhost:4317` |
| OTLP HTTP | `localhost:4318` |
| Collector Prometheus exporter | `http://localhost:9464/metrics` |

Credenciais locais do Grafana:

```text
admin / admin
```

## Como Subir

```powershell
docker compose up -d --build
docker compose ps
Invoke-RestMethod http://localhost:5074/health/ready
Invoke-RestMethod http://localhost:5276/health/ready
Invoke-RestMethod http://localhost:9090/-/ready
Invoke-RestMethod http://localhost:3000/api/health
```

O servico `kafka-init` aparecer como `Exited (0)` esta correto: ele cria os topicos e encerra.

## Logs Correlacionados

Os logs dos servicos .NET incluem `TraceId`/`SpanId` via `ActivityTrackingOptions`. O `Payment.Service` em Go tambem registra `traceId` e `spanId` nos logs estruturados.

Exemplo de consulta:

```powershell
docker compose logs --tail=180 order-service payment-service audit-worker | Select-String "<ORDER_ID>|TraceId|traceId"
```

## Traces

Abra o Jaeger:

```text
http://localhost:16686
```

Procure por:

- `MicroservicesLab.OrderService`;
- `MicroservicesLab.InventoryService`;
- `MicroservicesLab.PaymentService`;
- `MicroservicesLab.AuditWorker`.

Um trace saudavel de `POST /orders` deve conter spans semelhantes a:

- `POST /orders/`;
- `inventory.Inventory/CheckAvailability`;
- `POST /inventory.Inventory/CheckAvailability`;
- `inventory_db`;
- `order_db`;
- `Kafka produce order-created`;
- `Kafka consume order-created`;
- `process payment`;
- `Kafka produce payment-approved`;
- `Kafka consume payment-approved`;
- `Kafka audit order-created`;
- `Kafka audit payment-approved`;
- `audit_db`.

## Metricas

Prometheus:

```text
http://localhost:9090
```

Consultas uteis:

```promql
orders_created_total
orders_rejected_total
inventory_calls_total
inventory_dependency_failures_total
inventory_circuit_breaker_opens_total
payments_approved_total
payments_rejected_total
http_server_request_duration_seconds_count
```

Observacao: as metricas .NET podem levar cerca de 60 segundos para aparecer depois da primeira coleta OTLP.

## Lab Saudavel

Criar produto:

```powershell
$productBody = @{
  name = "Produto Observabilidade"
  price = 160.00
  availableQuantity = 5
} | ConvertTo-Json

$product = Invoke-RestMethod -Method Post -Uri http://localhost:5276/products -ContentType "application/json" -Body $productBody
$product | ConvertTo-Json -Depth 10
```

Criar pedido:

```powershell
$orderBody = @{
  productId = $product.id
  quantity = 1
} | ConvertTo-Json

$order = Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $orderBody
$order | ConvertTo-Json -Depth 10
```

Confirmar persistencia e auditoria:

```powershell
docker compose exec -T postgres psql -U postgres -d order_db -c "SELECT id, product_id, quantity, unit_price, total_price, status FROM orders WHERE id = '<ORDER_ID>';"
docker compose exec -T postgres psql -U postgres -d audit_db -c "SELECT event_type, topic, message_key, kafka_partition, kafka_offset FROM audit_events WHERE message_key = '<ORDER_ID>' ORDER BY audited_at;"
```

## Lab de Falha do Inventory

Crie um produto, pare o Inventory e tente criar pedidos:

```powershell
docker compose stop inventory-service

for ($i = 1; $i -le 4; $i++) {
  $body = @{ productId = $product.id; quantity = 1 } | ConvertTo-Json
  try {
    Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $body -TimeoutSec 15
  } catch {
    $_.Exception.Response.StatusCode.value__
  }
}
```

Resultado esperado:

- HTTP `503`;
- spans com erro no Jaeger;
- `orders_rejected_total{reason="inventory_unavailable"}`;
- `inventory_dependency_failures_total`;
- `inventory_circuit_breaker_opens_total`;
- nenhum pedido persistido para o produto durante a falha.

Religue o Inventory:

```powershell
docker compose start inventory-service
Invoke-RestMethod http://localhost:5276/health/ready
Start-Sleep -Seconds 12
```

Se o cliente gRPC do `Order.Service` mantiver uma resolucao antiga apos `docker compose stop/start`, reinicie apenas o `Order.Service`:

```powershell
docker compose restart order-service
Invoke-RestMethod http://localhost:5074/health/ready
```

Isso nao altera a arquitetura; apenas recria o canal gRPC dentro do container para validacao local.

## Evidencia Real da FASE 10

Fluxo saudavel validado:

```text
Produto: da168058-7f30-4813-bf9b-132964e929f7
Pedido: 36eb4ffe-fd94-421c-b7f4-d382d43b2447
TraceId: 07584288df11c4770b3250037e02a984
OrderCreated Kafka key: 36eb4ffe-fd94-421c-b7f4-d382d43b2447
PaymentApproved Kafka key: 36eb4ffe-fd94-421c-b7f4-d382d43b2447
Status final: Paid
```

Fluxo de recuperacao apos falha do Inventory:

```text
Produto: a3874583-49de-47e3-9fb3-7fe5a0a0e402
Pedido: 3c8354a1-0ae8-437d-a77d-add28452e0c8
TraceId: 9c6aa55a6a6728917044f0d41585a2f5
OrderCreated: topic order-created, partition 1, offset 2
PaymentApproved: topic payment-approved, partition 2, offset 2
Status final: Paid
```

Metricas observadas:

```text
orders_created_total = 2
payments_approved_total = 3
orders_rejected_total{reason="inventory_unavailable"} = 4
inventory_dependency_failures_total{reason="transient_failure"} = 3
inventory_dependency_failures_total{reason="circuit_open"} = 1
inventory_circuit_breaker_opens_total = 1
HTTP 503 em POST /orders = 4
```
