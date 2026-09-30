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

A Parte I esta concluida. A Parte II foi iniciada pela FASE 13 e segue sendo executada uma fase por vez, somente por instrucao explicita.

Entre a FASE 14 e a FASE 15 existe um mini-lab concluido de gRPC Server Streaming direto entre `PriceFeed.Service` e `Pricing.Service`. Ele e didatico e prepara a comparacao entre comunicacao direta e broker.

| Fase | Descricao | Status |
| ---- | --------- | ------ |
| 13 | PriceFeed.Service Go | Concluido |
| 14 | Realtime Delivery - SSE vs WebSocket | Concluido |
| Mini-lab | gRPC Server Streaming Go -> .NET | Concluido |
| 15 | Kafka PriceUpdated stream | Concluido |
| 16 | Pricing.Service .NET | Pendente |
| 17 | Concorrencia e backpressure | Pendente |
| 18 | Consumer scaling | Pendente |
| 19 | Multiplas instancias e partitioning | Pendente |
| 20 | Ingester/Processor | Pendente |
| 21 | Realtime observability | Pendente |
| 22 | Load e chaos testing | Pendente |

## Objetivo da Parte II

Estudar sistemas distribuidos realtime com foco em fluxo continuo de dados, concorrencia, throughput, backpressure, processamento distribuido, consumer lag, particionamento, escalabilidade e comportamento sob carga/falhas.

A progressao conceitual da Parte II e:

```text
gerar/ingerir dados continuamente
        |
        v
entregar dados em realtime
        |
        v
distribuir eventos
        |
        v
processar eventos
        |
        v
lidar com produtor mais rapido que consumidor
        |
        v
escalar consumidores
        |
        v
entender partitions e multiplas instancias
        |
        v
separar ingestao e processamento
        |
        v
observar o pipeline realtime
        |
        v
testar carga e falhas
```

Kubernetes, Kafka e observabilidade continuam importantes, mas na Parte II eles serao usados para estudar comportamento de streaming e distribuicao, nao apenas para repetir conceitos ja cobertos na Parte I.

## Blocos Conceituais da Parte II

| Bloco | Fases | Foco |
| ----- | ----- | ---- |
| A - Realtime | 13-16 | fluxo continuo, entrega realtime, Kafka e interoperabilidade Go -> .NET |
| B - Concorrencia e distribuicao | 17-19 | backpressure, consumer lag, scaling, partitions e multiplas instancias |
| C - Arquitetura | 20 | separacao entre ingestion e processing |
| D - Operacao | 21-22 | metricas realtime, carga, chaos e recuperacao |

O novo servico principal sera `PriceFeed.Service`, implementado em Go para explorar:

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
PriceProvider
          |
          v
   PriceFeed.Service
          Go
          |
          +-------> ConsolePublisher
          |
          +-------> Broadcaster
                   |
                   +-------> Realtime Delivery
                   |              SSE
                   |
                   +-------> gRPC Server Streaming
                                  Pricing.Service
          |
          +-------> KafkaPublisher
                   |
                   v
              price-updated
```

## Evolucao Planejada

```text
PriceProvider
      |
      v
PriceFeed.Service
      |
      v
   Publisher
      |
      v
    Kafka
 price-updated
      |
      v
Pricing.Service
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

A expansao nao dependera inicialmente de API financeira externa. A origem dos precos deve ficar atras de uma abstracao conceitual `PriceProvider`:

```text
PriceProvider
      |
      +-- MockPriceProvider
      |
      +-- fonte externa futura
```

Responsabilidades conceituais:

- `PriceProvider`: de onde vem os precos;
- `PriceFeed`: como o servico trabalha com o fluxo continuo;
- `Publisher`: para onde os precos sao enviados.

O `MockPriceProvider` inicial deve representar um pequeno mercado simulado com preco inicial, pequenas oscilacoes, multiplos simbolos e intervalo configuravel. A ideia nao e gerar valores totalmente aleatorios e independentes; e produzir uma serie plausivel:

```text
PETR4 = 38.10
PETR4 = 38.12
PETR4 = 38.09
PETR4 = 38.11
```

Simbolos candidatos para os labs:

```text
PETR4
VALE3
ITUB4
WEGE3
PRIO3
```

O intervalo de geracao devera permitir experimentos como `500ms`, `100ms`, `10ms` e `1ms`, respeitando os limites do ambiente local.

## Evento PriceUpdated

Contrato implementado na FASE 15:

```json
{
  "symbol": "PETR4",
  "price": 3815,
  "occurredAt": "2026-09-29T14:30:00Z"
}
```

O contrato e propositalmente simples para a fase: JSON independente de linguagem, com `price` em centavos (`int64`). Envelope completo, Schema Registry e Protobuf no Kafka ficam fora do escopo.

Na FASE 15, `symbol` e a Kafka message key. Isso permite estudar ordenacao por ativo dentro de uma partition:

```text
PETR4 -> mesma partition para eventos de PETR4
VALE3 -> possivelmente outra partition
```

Essa decisao devera ser validada na implementacao, porque uma key tambem influencia distribuicao de carga.

## Planejamento das Fases 13-22

| Fase | Nome | Objetivo principal | Conceitos estudados | Status |
| ---- | ---- | ------------------ | ------------------- | ------ |
| 13 | PriceFeed.Service Go | Criar um servico Go responsavel por fornecer um fluxo continuo de precos, iniciando com fonte simulada dinamica e desacoplada. | goroutines, channels, `context.Context`, fluxo continuo, configuracao, `PriceProvider`, `Publisher` | Concluido |
| 14 | Realtime Delivery - SSE vs WebSocket | Estudar requisitos de entrega backend -> cliente e escolher conscientemente SSE para o fluxo atual, sem implementar WebSocket. | SSE, fluxo unidirecional, conexoes longas, clientes realtime, broadcasting | Concluido |
| Mini-lab | gRPC Server Streaming Go -> .NET | Comparar streaming direto tipado com SSE antes de introduzir Kafka. | `.proto`, HTTP/2, server streaming, cancelamento por contexto, interoperabilidade Go -> .NET | Concluido |
| 15 | Kafka PriceUpdated stream | Distribuir o fluxo de precos por Kafka usando eventos `PriceUpdated`. | producer, broker, topic, partitions, ordering por key, `symbol` como key, stream de eventos | Concluido |
| 16 | Pricing.Service .NET | Consumir `price-updated` em .NET e manter ultimo preco conhecido por simbolo. | interoperabilidade Go -> Kafka -> .NET, contratos independentes de linguagem, estado derivado simples | Pendente |
| 17 | Concorrencia e backpressure | Criar situacoes em que o producer e mais rapido que o consumer e observar limites do pipeline. | goroutines, buffered/unbuffered channels, worker pool, fan-out/fan-in, buffer cheio, consumer lag | Pendente |
| 18 | Consumer scaling | Estudar como replicas de consumers se relacionam com partitions dentro de um consumer group. | consumer groups, partition assignment, rebalance, consumers x partitions, scaling util versus inutil | Pendente |
| 19 | Multiplas instancias e partitioning | Aprofundar como Kafka distribui trabalho e preserva ordenacao com multiplas instancias. | key, partition, ownership, ordering por simbolo, rebalance, comportamento distribuido real | Pendente |
| 20 | Ingester/Processor | Separar conceitualmente ingestao e processamento do pipeline realtime. | ingestion, normalization, validation, processing, aggregation, estado derivado | Pendente |
| 21 | Realtime observability | Usar a observabilidade existente para responder perguntas especificas de streaming. | events/s, latencia, consumer lag, buffer usage, drops, active connections, dashboard realtime | Pendente |
| 22 | Load e chaos testing | Avaliar ate onde o sistema funciona e como falha sob carga e falhas controladas. | throughput, latencia, CPU, memoria, lag, recuperacao, degradacao, chaos controlado | Pendente |

## Distincao Entre FASE 18 e FASE 19

A FASE 18 responde:

```text
O que acontece quando aumentamos consumers?
```

Ela compara cenarios como:

```text
Topic: 3 partitions

1 consumer  -> 3 partitions
2 consumers -> 3 partitions distribuidas
3 consumers -> 1 partition por consumer
5 consumers -> 2 consumers ociosos no mesmo group
```

A FASE 19 responde:

```text
Como Kafka distribui trabalho e preserva ordenacao quando existem multiplas instancias?
```

Ela aprofunda `key`, `partition`, `consumer group`, `partition ownership`, `ordering` e `rebalance`, usando `symbol` como exemplo natural.

## Laboratorios Realtime Planejados

Os laboratorios serao adicionados posteriormente em `docs/labs/realtime/`. A lista abaixo e planejada e podera ser ajustada durante cada fase:

```text
LAB-RT-01-price-provider.md
LAB-RT-02-realtime-delivery.md
LAB-RT-03-kafka-price-updated.md
LAB-RT-04-pricing-service.md
LAB-RT-05-backpressure.md
LAB-RT-06-consumer-scaling.md
LAB-RT-07-partitioning-and-ordering.md
LAB-RT-08-ingester-processor.md
LAB-RT-09-realtime-observability.md
LAB-RT-10-load-and-chaos.md
```

## Fora de Escopo por Enquanto

Nao adicionar tecnologias apenas para aumentar a stack. Redis, RabbitMQ, MongoDB, Elasticsearch, API Gateway, Service Mesh, Helm, ArgoCD, Terraform ou cloud deverao ser avaliados somente se uma necessidade concreta aparecer em fase futura.
