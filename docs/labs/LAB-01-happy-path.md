# LAB 01 - Happy Path

## Objetivo

Criar um produto, criar um pedido, observar `OrderCreated`, pagamento aprovado, status final `Paid` e auditoria.

## Pre-Requisitos

Ambiente Docker Compose rodando:

```powershell
docker compose up -d --build
```

## Passos

Criar produto:

```powershell
$productBody = @{
  name = "Produto Lab Happy Path"
  price = 150.00
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

Aguardar consumidores e consultar:

```powershell
Start-Sleep -Seconds 8
Invoke-RestMethod "http://localhost:5074/orders/$($order.id)" | ConvertTo-Json -Depth 10
```

Confirmar no banco:

```powershell
docker compose exec -T postgres psql -U postgres -d order_db -c "SELECT id, product_id, quantity, total_price, status FROM orders WHERE id = '$($order.id)';"
docker compose exec -T postgres psql -U postgres -d audit_db -c "SELECT event_type, topic, message_key, kafka_partition, kafka_offset FROM audit_events WHERE message_key = '$($order.id)' ORDER BY audited_at;"
```

## O Que Observar

- `POST /orders` retorna `201`.
- O pedido nasce `Pending`.
- `Payment.Service` consome `OrderCreated`.
- `Payment.Service` publica `PaymentApproved`.
- `Order.Service` atualiza o pedido para `Paid`.
- `Audit.Worker` grava `OrderCreated` e `PaymentApproved`.

## Resultado Esperado

```text
orders.status = Paid
audit_events: OrderCreated + PaymentApproved
Kafka key = OrderId
```

## Limpeza

Nao ha limpeza obrigatoria. Para apagar dados locais do Compose:

```powershell
docker compose down -v
```

## Conceito Demonstrado

Comunicacao sincrona por gRPC, comunicacao assincrona por Kafka e consistencia eventual no status do pedido.
