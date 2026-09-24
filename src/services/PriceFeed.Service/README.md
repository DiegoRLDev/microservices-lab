# PriceFeed.Service

`PriceFeed.Service` e o servico Go introduzido na FASE 13 da Parte II - Realtime Distributed Systems.

Ele produz um fluxo continuo de atualizacoes de precos usando uma fonte simulada (`MockPriceProvider`) e publica localmente no console. Kafka, SSE, WebSocket, consumidores .NET, Kubernetes e testes de carga ficam para fases futuras.

## Responsabilidades

- `PriceProvider`: origem dos precos. Nesta fase, `MockPriceProvider`.
- `PriceFeed`: consome o fluxo do provider e encaminha para um publisher.
- `Publisher`: destino das atualizacoes. Nesta fase, `ConsolePublisher`.

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
```

Todas possuem valores padrao.

## Executar Localmente

```powershell
cd .\src\services\PriceFeed.Service
go run .
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
