# Roadmap do MicroservicesLab

Este e o roadmap oficial do laboratorio. As fases devem ser executadas uma por vez, com parada ao final de cada fase.

## Parte I - Microservices Fundamentals

| Fase | Descricao | Status |
| ---- | --------- | ------ |
| 0 | Estrutura | Concluido |
| 1 | Order Service | Concluido |
| 2 | Inventory Service | Concluido |
| 3 | gRPC | Concluido |
| 4 | Kafka producer | Concluido |
| 4.1 | Validacao Kafka real | Concluido |
| 5 | Payment Go | Concluido |
| 6 | Eventos pagamento | Concluido |
| 7 | Audit Worker | Concluido |
| 8 | Docker Compose | Concluido |
| 9 | Resiliencia | Concluido |
| 10 | Observabilidade | Concluido |
| 11 | Kubernetes | Concluido |
| 12 | Labs/documentacao | Concluido |

## Parte II - Realtime Distributed Systems

A Parte I esta concluida. A Parte II esta registrada para orientar a proxima trilha do laboratorio, mas nao deve ser implementada sem instrucao explicita para a FASE 13.

| Fase | Descricao | Status |
| ---- | --------- | ------ |
| 13 | Realtime.PriceFeed Go | Pendente |
| 14 | WebSocket/SSE | Pendente |
| 15 | Kafka PriceUpdated | Pendente |
| 16 | Pricing.Service .NET | Pendente |
| 17 | Concorrencia/backpressure | Pendente |
| 18 | Kubernetes scaling | Pendente |
| 19 | Multiplas instancias PriceFeed | Pendente |
| 20 | Ingester/Processor | Pendente |
| 21 | Observabilidade realtime | Pendente |
| 22 | Load/Chaos testing | Pendente |

## Objetivo da Parte II

Estudar processamento de dados em tempo real usando Go, Kafka, WebSocket/SSE, Kubernetes e observabilidade.

O novo servico principal sera `Realtime.PriceFeed`, implementado em Go para explorar:

- processamento concorrente;
- goroutines;
- channels;
- streaming;
- conexoes de longa duracao;
- alto volume de mensagens;
- baixo overhead;
- integracao com Kafka;
- escalabilidade horizontal.

## Arquitetura Conceitual Inicial

```text
External Price Provider
          |
          | streaming / polling
          v
 Realtime.PriceFeed
          Go
          |
          +-------> Kafka
          |       price-updated
          |
          +-------> WebSocket / SSE
                       |
                       v
                    Clients
```

## Evolucao Planejada

```text
External Price Provider
          |
          v
   Realtime.PriceFeed
          Go
          |
          v
        Kafka
    price-updated
          |
     +----+----------------+
     |                     |
     v                     v
Pricing.Service       outros consumers
     .NET
```

Essa evolucao demonstra interoperabilidade:

```text
Go
 |
 | evento
 v
Kafka
 |
 v
.NET
```

Nenhum servico deve depender da linguagem utilizada pelo outro. A integracao deve ocorrer atraves de contratos independentes de linguagem.

## Fonte de Precos

A expansao nao dependera inicialmente de API financeira externa. Um `Price Generator` simulara atualizacoes como:

```text
PETR4 -> 32.45
PETR4 -> 32.47
VALE3 -> 71.20
BTCUSD -> 68234.00
```

O gerador devera permitir controle de frequencia com configuracao semelhante a `PRICE_EVENTS_PER_SECOND=100`.

## Evento PriceUpdated

Contrato inicial planejado:

```json
{
  "eventId": "uuid",
  "eventType": "PriceUpdated",
  "symbol": "PETR4",
  "price": 32.45,
  "currency": "BRL",
  "occurredAt": "timestamp",
  "correlationId": "uuid"
}
```

O contrato definitivo podera ser refinado durante a implementacao, mantendo independencia de linguagem.

## Laboratorios Realtime Planejados

Os laboratorios serao adicionados posteriormente em `docs/labs/realtime/`:

```text
LAB-RT-01-price-generator.md
LAB-RT-02-goroutines.md
LAB-RT-03-channels.md
LAB-RT-04-websocket-sse.md
LAB-RT-05-kafka-producer.md
LAB-RT-06-go-to-dotnet.md
LAB-RT-07-load-test.md
LAB-RT-08-backpressure.md
LAB-RT-09-kafka-lag.md
LAB-RT-10-kubernetes-scaling.md
LAB-RT-11-multiple-pricefeeds.md
LAB-RT-12-ingester-processor.md
LAB-RT-13-observability.md
LAB-RT-14-chaos.md
```
