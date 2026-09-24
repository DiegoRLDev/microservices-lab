# Guia de Resiliencia

Este guia registra a FASE 9 do MicroservicesLab. O foco desta fase e tornar previsivel a dependencia sincrona `Order.Service -> Inventory.Service`.

## Escopo da FASE 9

Implementado:

- timeout por chamada gRPC de disponibilidade de estoque;
- retry limitado para falhas transientes do gRPC;
- circuit breaker em memoria no `Order.Service`;
- resposta HTTP `503 Service Unavailable` quando o `Inventory.Service` esta indisponivel;
- logs para tentativas, retries, abertura, half-open e fechamento do circuito;
- configuracao por `appsettings.json` e variaveis de ambiente no Docker Compose.

Fora do escopo desta fase:

- Outbox Pattern;
- Dead Letter Queue;
- Service Mesh;
- observabilidade com OpenTelemetry;
- resiliencia completa para o fluxo Kafka.

## Configuracao

Configuracao padrao em `Order.Service`:

```json
{
  "Resilience": {
    "Inventory": {
      "TimeoutSeconds": 2,
      "RetryAttempts": 2,
      "RetryDelayMilliseconds": 200,
      "CircuitBreakerFailureThreshold": 3,
      "CircuitBreakerOpenSeconds": 10
    }
  }
}
```

No Docker Compose, os mesmos valores sao aplicados por variaveis `Resilience__Inventory__...`.

## Comportamento Esperado

Quando o `Inventory.Service` esta disponivel:

```text
POST /orders
  -> Order.Service
  -> gRPC CheckAvailability
  -> Inventory.Service
  -> SaveChanges em order_db
  -> OrderCreated em Kafka
```

Quando o `Inventory.Service` esta indisponivel:

```text
POST /orders
  -> timeout/retry limitado
  -> circuit breaker abre apos falhas consecutivas
  -> HTTP 503
  -> pedido nao e persistido
  -> OrderCreated nao e publicado
```

Quando o circuito esta aberto, chamadas seguintes falham rapido com `503`. Depois da janela configurada, uma chamada de teste entra em half-open. Se ela tiver sucesso, o circuito fecha.

## Lab: Inventory Indisponivel

Suba o ambiente completo:

```powershell
docker compose up -d --build
docker compose ps
Invoke-RestMethod http://localhost:5276/health/ready
Invoke-RestMethod http://localhost:5074/health/ready
```

Crie um produto:

```powershell
$body = @{ name = "Produto Resiliencia"; price = 140.00; availableQuantity = 5 } | ConvertTo-Json
$product = Invoke-RestMethod -Method Post -Uri http://localhost:5276/products -ContentType "application/json" -Body $body
$product | ConvertTo-Json -Depth 5
```

Pare somente o Inventory:

```powershell
docker compose stop inventory-service
```

Tente criar pedidos:

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

Resultado esperado:

- as chamadas retornam `503`;
- os logs mostram `Attempt 1/3`, retries e abertura do circuit breaker;
- pelo menos uma chamada apos abertura do circuito falha em poucos milissegundos;
- nenhum pedido e salvo no `order_db` para esse produto.

Consultar o banco:

```powershell
docker compose exec -T postgres psql -U postgres -d order_db -c "SELECT id, product_id, status, created_at FROM orders WHERE product_id = '<PRODUCT_ID>';"
```

Ver logs:

```powershell
docker compose logs --tail=120 order-service
```

Religue o Inventory e aguarde a janela do circuit breaker:

```powershell
docker compose start inventory-service
Start-Sleep -Seconds 12
Invoke-RestMethod http://localhost:5276/health/ready
```

Crie um pedido novamente. O primeiro sucesso apos a janela deve fechar o circuito:

```powershell
$body = @{ productId = $product.id; quantity = 1 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $body -TimeoutSec 20 | ConvertTo-Json -Depth 10
```

## Lab: Kafka Indisponivel

Este cenario continua propositalmente documentado como limite arquitetural da fase atual.

```powershell
docker compose stop kafka
```

Com Kafka parado, `Order.Service` ainda consegue consultar o Inventory e salvar o pedido. Em seguida, a publicacao de `OrderCreated` nao conclui e o cliente pode observar timeout.

Resultado observado na FASE 9:

```text
HTTP client: timeout apos 25 segundos
order_db: pedido salvo como Pending
Kafka: OrderCreated nao publicado naquele momento
```

Esse comportamento reforca o trade-off ja registrado desde a FASE 4: PostgreSQL e Kafka nao participam da mesma transacao. A solucao adequada para esse ponto sera o Outbox Pattern em uma fase futura, nao nesta.

Depois do teste, restaure o Kafka e os servicos:

```powershell
docker compose start kafka
Start-Sleep -Seconds 15
docker compose up -d
docker compose ps
```

## Validacao Real da FASE 9

Evidencias executadas no Compose completo:

```text
Produto de resiliencia:
ea15e930-1370-4918-abb1-557e5062bbb4 - Produto Resiliencia Fase 9

Inventory offline:
HTTP 503 em POST /orders
Circuit breaker aberto por 10 segundos
Pedido nao persistido para o produto durante a falha

Recuperacao:
Pedido criado: d90733cb-1a48-47b9-924b-27191d0030f2
Status final no order_db: Paid
OrderCreated: 0809c2eb-2bc1-4a09-84de-a8f2db86e708
Kafka key: d90733cb-1a48-47b9-924b-27191d0030f2

Kafka offline:
Produto: 068bc05b-ad4a-4d4a-8618-dc94d087b4d0
Pedido salvo: 68c71987-8076-493d-88db-4aad06b186da
Status: Pending
Cliente: timeout apos 25 segundos
```

Evento `OrderCreated` real da recuperacao:

```json
{
  "eventId": "0809c2eb-2bc1-4a09-84de-a8f2db86e708",
  "eventType": "OrderCreated",
  "occurredAt": "2026-09-11T17:34:53.2100512+00:00",
  "correlationId": "90ec34d1324f31dc54e3e740cc0e618b",
  "data": {
    "orderId": "d90733cb-1a48-47b9-924b-27191d0030f2",
    "productId": "ea15e930-1370-4918-abb1-557e5062bbb4",
    "quantity": 1,
    "unitPrice": 140.00,
    "totalPrice": 140.00,
    "status": "Pending",
    "createdAt": "2026-09-11T17:34:53.1876374+00:00"
  }
}
```
