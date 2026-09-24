# LAB 04 - Kafka

## Objetivo

Listar topicos, consumir eventos, visualizar key e payload, e relacionar topic, partition, offset, key e consumer group ao fluxo do projeto.

## Pre-Requisitos

Docker Compose rodando:

```powershell
docker compose up -d --build
```

## Passos

Listar topicos:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list
```

Consumir `order-created`:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:29092 --topic order-created --from-beginning --property print.key=true --property key.separator=" | "
```

Consumir `payment-approved`:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:29092 --topic payment-approved --from-beginning --property print.key=true --property key.separator=" | "
```

Consumir `payment-rejected`:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:29092 --topic payment-rejected --from-beginning --property print.key=true --property key.separator=" | "
```

Criar um pedido em outro terminal seguindo o [LAB 01](LAB-01-happy-path.md).

## O Que Observar

- `topic`: fila logica de eventos, como `order-created`.
- `partition`: subdivisao do topico.
- `offset`: posicao da mensagem na partition.
- `key`: no projeto, geralmente o `OrderId`.
- `consumer group`: identifica leitores que compartilham o consumo.

## Resultado Esperado

```text
order-created: key = OrderId, payload = envelope OrderCreated
payment-approved/payment-rejected: key = OrderId, payload = envelope de pagamento
```

## Limpeza

Encerrar o consumidor com `Ctrl+C`.

## Conceito Demonstrado

Mensageria assincrona, ordenacao por key dentro de partition e interoperabilidade JSON entre .NET e Go.
