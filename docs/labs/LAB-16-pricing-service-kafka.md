# LAB 16 - Pricing.Service Kafka

Este lab valida a transformacao:

```text
event stream
    |
    v
estado derivado atual
```

Fluxo:

```text
PriceFeed.Service
        |
        | PriceUpdated
        v
      Kafka
   price-updated
        |
        v
Pricing.Service
        |
        +-- LatestPriceStore
        +-- GET /prices
        +-- GET /prices/{symbol}
```

## Contrato

`PriceUpdated` e JSON independente de linguagem:

```json
{
  "symbol": "PETR4",
  "price": 4893,
  "occurredAt": "2026-10-01T13:30:01.4110091Z"
}
```

`price` representa centavos. A Kafka message key deve ser o `symbol`.

## Configuracao

Local:

```text
Kafka:BootstrapServers = localhost:9092
Kafka:Topic = price-updated
Kafka:ConsumerGroup = pricing-service
```

Docker Compose:

```text
Kafka__BootstrapServers = kafka:29092
Kafka__Topic = price-updated
Kafka__ConsumerGroup = pricing-service
```

O consumer usa `AutoOffsetReset=Earliest` e commit manual depois de atualizar o estado em memoria. Um consumer group novo le desde o inicio do log retido. Um consumer group existente retoma dos offsets ja commitados.

## A - Subir Kafka

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
powershell -ExecutionPolicy Bypass -File .\infra\kafka\create-topics.ps1
```

Confirmar o topico:

```powershell
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --describe --topic price-updated
```

## B - Iniciar PriceFeed.Service

```powershell
cd .\src\services\PriceFeed.Service
$env:PRICE_KAFKA_ENABLED = "true"
$env:PRICE_KAFKA_BOOTSTRAP_SERVERS = "localhost:9092"
$env:PRICE_KAFKA_TOPIC = "price-updated"
go run .
```

## C - Iniciar Pricing.Service

Em outro terminal:

```powershell
cd .\src\services\Pricing.Service
dotnet run
```

## D - Consultar todos os precos

```powershell
Invoke-RestMethod http://localhost:5000/prices
```

Resultado esperado: lista com os ultimos precos conhecidos por simbolo.

## E - Consultar um simbolo

```powershell
Invoke-RestMethod http://localhost:5000/prices/PETR4
```

Se o simbolo ainda nao foi consumido, a API retorna `404 Not Found`.

## F - Observar mudanca do estado

Execute varias vezes:

```powershell
Invoke-RestMethod http://localhost:5000/prices/PETR4
```

O valor de `price` e/ou `occurredAt` deve mudar conforme novos eventos chegam.

## G - Parar e reiniciar Pricing.Service

1. Pare `Pricing.Service` com `Ctrl+C`.
2. Mantenha `PriceFeed.Service` produzindo por alguns segundos.
3. Inicie `Pricing.Service` novamente.
4. Consulte:

```powershell
Invoke-RestMethod http://localhost:5000/prices
```

Como existe Kafka entre produtor e consumidor, o produtor pode continuar publicando enquanto o `Pricing.Service` esta offline. Na volta, o comportamento de retomada depende dos offsets do consumer group `pricing-service`: grupos novos iniciam em `Earliest`; grupos existentes retomam dos offsets commitados.

## Diferenca didatica: gRPC direto vs Kafka

gRPC Server Streaming:

```text
PriceFeed.Service -------- conexao direta --------> Pricing.Service
```

Se `Pricing.Service` estiver offline, ele nao esta conectado ao stream naquele periodo.

Kafka:

```text
PriceFeed.Service -> Kafka -> Pricing.Service
```

Produtor e consumidor ficam temporalmente desacoplados. Kafka mantem eventos no broker conforme retencao e offsets configurados.

## Docker Compose

No Compose completo:

```powershell
docker compose up -d --build
Invoke-RestMethod http://localhost:5090/prices
Invoke-RestMethod http://localhost:5090/prices/PETR4
```

Porta host do `Pricing.Service`: `5090`.
