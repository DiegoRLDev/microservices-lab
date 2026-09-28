# PriceFeed.Service

`PriceFeed.Service` e o servico Go introduzido na FASE 13 da Parte II - Realtime Distributed Systems.

Ele produz um fluxo continuo de atualizacoes de precos usando uma fonte simulada (`MockPriceProvider`), publica localmente no console e entrega o mesmo fluxo para clientes externos via SSE. Kafka, WebSocket, gRPC streaming, consumidores .NET, Kubernetes e testes de carga ficam para fases futuras.

## Responsabilidades

- `PriceProvider`: origem dos precos. Nesta fase, `MockPriceProvider`.
- `PriceFeed`: consome o fluxo do provider e encaminha para um publisher.
- `Publisher`: destino das atualizacoes. Nesta fase, `ConsolePublisher` e `Broadcaster` por meio de `MultiPublisher`.
- `Broadcaster`: distribui cada atualizacao para subscribers independentes.

## MockPriceProvider

O mock mantem estado por simbolo. Cada simbolo parte de um preco inicial e, a cada intervalo, recebe uma pequena variacao em centavos dentro do limite configurado.

Simbolos padrao:

```text
PETR4
VALE3
ITUB4
WEGE3
PRIO3
```

Precos iniciais padrao:

```text
PETR4=38.10
VALE3=74.21
ITUB4=42.95
WEGE3=39.80
PRIO3=48.35
```

Regra de variacao padrao:

```text
novo preco = preco atual + variacao aleatoria entre -5 e +5 centavos
```

O intervalo padrao e `500ms`.

## Representacao de Preco

Os precos sao representados em centavos (`int64`), por exemplo `3810` para `38.10`.

Essa escolha evita problemas basicos de arredondamento de `float64` sem adicionar uma biblioteca decimal externa, o que seria excesso para esta fase didatica.

## Configuracao

Variaveis de ambiente:

```text
PRICE_PROVIDER=mock
PRICE_SYMBOLS=PETR4,VALE3,ITUB4,WEGE3,PRIO3
PRICE_UPDATE_INTERVAL=500ms
PRICE_INITIAL_PRICES=PETR4=38.10,VALE3=74.21,ITUB4=42.95,WEGE3=39.80,PRIO3=48.35
PRICE_MAX_STEP_CENTS=5
PRICE_HTTP_ADDR=:8080
```

Todas possuem valores padrao.

## HTTP e SSE

Endpoints:

```text
GET /health
GET /prices/stream
```

`/prices/stream` usa SSE com:

```text
Content-Type: text/event-stream
```

Cada atualizacao e enviada como:

```text
event: price-update
data: {"symbol":"PETR4","price":3810,"occurredAt":"2026-09-28T14:30:01Z"}
```

`price` permanece em centavos, preservando a representacao interna da FASE 13.

SSE foi escolhido porque o fluxo atual e unidirecional: servidor para cliente. WebSocket nao foi implementado porque ainda nao existe requisito de comunicacao bidirecional continua.

## Broadcasting e Clientes Lentos

Cada cliente SSE cria um subscriber proprio no `Broadcaster`. Ao desconectar, o request context e cancelado, o subscriber e removido e seu channel e fechado.

Cada subscriber possui um channel bufferizado. Se um cliente consumir devagar e o buffer encher, novas atualizacoes para aquele cliente sao descartadas. Essa politica simples evita que um cliente lento bloqueie todos os demais. Backpressure completo fica para a FASE 17.

## Executar Localmente

```powershell
cd .\src\services\PriceFeed.Service
go run .
```

Verificar health:

```powershell
curl http://localhost:8080/health
```

Consumir SSE:

```powershell
curl -N http://localhost:8080/prices/stream
```

Para observar frequencia maior:

```powershell
$env:PRICE_UPDATE_INTERVAL = "100ms"
go run .
```

Use `Ctrl+C` para cancelar. O servico cancela o contexto, para os produtores, fecha o channel e encerra normalmente.

## Testes

```powershell
cd .\src\services\PriceFeed.Service
go test ./...
go test -race ./...
```
