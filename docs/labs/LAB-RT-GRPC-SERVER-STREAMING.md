# Mini-lab Realtime - gRPC Server Streaming

Este mini-lab demonstra uma comunicacao direta de streaming entre:

```text
PriceFeed.Service (Go) -> gRPC Server Streaming -> Pricing.Service (.NET)
```

Ele fica entre a FASE 14 e a FASE 15. Nao substitui Kafka e nao implementa o fluxo `price-updated`; Kafka continua pendente para a FASE 15.

## Conceito

REST tradicional usa o modelo:

```text
request -> response
```

gRPC Server Streaming usa:

```text
request -> response, response, response...
```

O cliente envia uma unica requisicao `StreamPrices` e o servidor mantem a conexao aberta, enviando varios `PriceUpdate` enquanto o contexto do cliente continuar ativo.

## LAB A - Contrato e Conexao

Contrato:

```text
src/protos/pricefeed.proto
```

O contrato define:

```text
service PriceFeed {
  rpc StreamPrices(StreamPricesRequest) returns (stream PriceUpdate);
}
```

`PriceUpdate` contem:

```text
symbol
price
occurred_at
```

`price` e `int64` em centavos, mantendo a decisao da FASE 13.

## LAB B - Streaming Real

Terminal 1:

```powershell
cd .\src\services\PriceFeed.Service
$env:PRICE_GRPC_ADDR = ":9090"
$env:PRICE_HTTP_ADDR = ":8080"
go run .
```

Terminal 2:

```powershell
cd .\src\services\Pricing.Service
$env:PRICE_FEED_GRPC_ADDRESS = "http://localhost:9090"
dotnet run
```

Saida esperada no cliente:

```text
[gRPC] PETR4 38.11 2026-09-28T14:30:01.1020000Z
[gRPC] VALE3 74.18 2026-09-28T14:30:01.1830000Z
[gRPC] ITUB4 42.98 2026-09-28T14:30:01.4310000Z
```

Os simbolos e valores variam porque o provider simulado gera pequenas oscilacoes continuas.

## LAB C - Cancelamento

Com os dois processos rodando, pressione `Ctrl+C` no terminal do `Pricing.Service`.

Resultado esperado:

```text
Pricing.Service gRPC lab client stopped.
```

O cliente cancela o contexto da chamada gRPC. O `PriceFeed.Service` remove o subscriber do `Broadcaster`, fecha o channel daquele cliente e continua publicando para console, SSE e outros subscribers.

Para encerrar o servidor, pressione `Ctrl+C` no terminal do `PriceFeed.Service`.

## LAB D - SSE vs gRPC Server Streaming

| Criterio | SSE | gRPC Server Streaming |
| --- | --- | --- |
| Direcao | Servidor -> cliente | Servidor -> cliente |
| Transporte | HTTP/event-stream textual | HTTP/2 + Protobuf |
| Cliente natural | Browser, curl, ferramentas HTTP | Servicos backend e clientes gRPC |
| Contrato | JSON simples no evento SSE | `.proto` fortemente tipado |
| Uso neste lab | Entrega realtime para clientes externos simples | Comunicacao interna didatica Go -> .NET |
| Bidirecional | Nao | Nao neste modo |

Neste momento, os dois transports recebem o mesmo fluxo via `Broadcaster`. Kafka continua fora deste mini-lab para preservar a diferenca conceitual entre streaming direto e mensageria por broker.
