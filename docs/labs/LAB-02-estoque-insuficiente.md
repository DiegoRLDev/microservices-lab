# LAB 02 - Estoque Insuficiente

## Objetivo

Confirmar que um pedido acima do estoque disponivel nao segue o fluxo normal.

## Pre-Requisitos

Docker Compose rodando:

```powershell
docker compose up -d --build
```

## Passos

Criar produto com estoque limitado:

```powershell
$productBody = @{
  name = "Produto Estoque Limitado"
  price = 80.00
  availableQuantity = 1
} | ConvertTo-Json

$product = Invoke-RestMethod -Method Post -Uri http://localhost:5276/products -ContentType "application/json" -Body $productBody
```

Tentar criar pedido acima do estoque:

```powershell
$orderBody = @{
  productId = $product.id
  quantity = 2
} | ConvertTo-Json

try {
  Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $orderBody
} catch {
  $_.Exception.Response.StatusCode.value__
}
```

Verificar se nao houve pedido:

```powershell
docker compose exec -T postgres psql -U postgres -d order_db -c "SELECT id, product_id, status FROM orders WHERE product_id = '$($product.id)';"
```

Verificar se nao houve auditoria para o produto/pedido, ja que nenhum pedido foi criado:

```powershell
docker compose exec -T postgres psql -U postgres -d audit_db -c "SELECT event_type, message_key FROM audit_events ORDER BY audited_at DESC LIMIT 5;"
```

## O Que Observar

- `Order.Service` chama `Inventory.Service` via gRPC.
- Inventory retorna disponibilidade insuficiente.
- `Order.Service` retorna erro de negocio.
- O pedido nao e persistido.
- `OrderCreated` nao e publicado.

## Resultado Esperado

```text
HTTP 400
orders: nenhum registro para o produto desse lab
Kafka: nenhum OrderCreated desse pedido
```

## Limpeza

Nao ha limpeza obrigatoria.

## Conceito Demonstrado

Regra de negocio bloqueando o fluxo antes de persistir pedido e antes de publicar evento.
