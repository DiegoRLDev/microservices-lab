# LAB 03 - Resiliencia com Inventory Offline

## Objetivo

Observar timeout, retry, circuit breaker e recuperacao quando `Inventory.Service` fica indisponivel.

## Pre-Requisitos

Docker Compose rodando:

```powershell
docker compose up -d --build
Invoke-RestMethod http://localhost:5276/health/ready
Invoke-RestMethod http://localhost:5074/health/ready
```

## Passos

Criar produto:

```powershell
$productBody = @{
  name = "Produto Resiliencia Lab"
  price = 140.00
  availableQuantity = 5
} | ConvertTo-Json

$product = Invoke-RestMethod -Method Post -Uri http://localhost:5276/products -ContentType "application/json" -Body $productBody
```

Parar Inventory:

```powershell
docker compose stop inventory-service
```

Tentar criar pedidos:

```powershell
for ($i = 1; $i -le 4; $i++) {
  $body = @{ productId = $product.id; quantity = 1 } | ConvertTo-Json
  try {
    Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $body -TimeoutSec 15
  } catch {
    $_.Exception.Response.StatusCode.value__
  }
}
```

Ver logs:

```powershell
docker compose logs --tail=160 order-service
```

Verificar que nao houve persistencia:

```powershell
docker compose exec -T postgres psql -U postgres -d order_db -c "SELECT id, product_id, status FROM orders WHERE product_id = '$($product.id)';"
```

Religar Inventory e aguardar a janela do breaker:

```powershell
docker compose start inventory-service
Invoke-RestMethod http://localhost:5276/health/ready
Start-Sleep -Seconds 12
```

Criar pedido novamente:

```powershell
$body = @{ productId = $product.id; quantity = 1 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $body -TimeoutSec 20 | ConvertTo-Json -Depth 10
```

## O Que Observar

- primeiras chamadas fazem retry;
- depois o circuit breaker abre;
- chamadas seguintes falham rapido;
- HTTP esperado durante falha: `503`;
- nenhum pedido e salvo durante indisponibilidade;
- depois da recuperacao o fluxo volta a funcionar.

## Resultado Esperado

```text
Inventory offline: HTTP 503
Pedido nao persistido durante falha
Inventory online novamente: pedido volta a ser criado
```

## Limpeza

Se o Inventory ficar parado:

```powershell
docker compose start inventory-service
```

## Conceito Demonstrado

Resiliencia no caminho sincrono `Order.Service -> Inventory.Service`.
