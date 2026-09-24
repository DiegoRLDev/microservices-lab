# Guia Kafka

Este guia registra como o Kafka e usado no MicroservicesLab hoje. Ele nao e um manual generico de Kafka; os comandos e nomes abaixo refletem os arquivos e servicos reais do projeto.

## Arquitetura Atual

```text
Order.Service
     |
     | OrderCreated
     v
order-created
     |
     v
Payment.Service
     |
     +---- PaymentApproved ---> payment-approved
     |
     +---- PaymentRejected ---> payment-rejected

Audit.Worker
     |
     +---- consome order-created
     +---- consome payment-approved
     +---- consome payment-rejected
```

Na FASE 8, `Order.Service` publica `OrderCreated`, `Payment.Service` consome esse evento e publica `PaymentApproved` ou `PaymentRejected`. Depois, `Order.Service` consome o evento de pagamento e atualiza o status do pedido. `Audit.Worker` consome os tres topicos ativos e grava os envelopes em `audit_db`.

Na FASE 9, a resiliencia foi aplicada ao caminho sincrono `Order.Service -> Inventory.Service`. O comportamento com Kafka indisponivel foi apenas observado e documentado: sem Outbox Pattern, um pedido pode ser salvo como `Pending` antes de `OrderCreated` ser publicado.

## Topicos Atuais

```text
order-created
payment-approved
payment-rejected
```

No Compose completo, os topicos sao criados pelo servico `kafka-init` em [docker-compose.yml](../../docker-compose.yml). No broker isolado antigo, os topicos continuam podendo ser criados por [infra/kafka/create-topics.ps1](../../infra/kafka/create-topics.ps1).

No Compose completo, containers usam `kafka:29092`. Para inspecao pelo host, use `localhost:9092`.

## Conceitos Rapidos

- `topic`: canal nomeado onde eventos ficam armazenados. Exemplo: `order-created`.
- `message/event`: registro gravado no Kafka. No projeto, a mensagem e um JSON com envelope `eventId`, `eventType`, `occurredAt`, `correlationId` e `data`.
- `key`: chave da mensagem. Aqui usamos `OrderId`, para manter eventos do mesmo pedido na mesma partition.
- `partition`: divisao fisica/logica de um topic. Permite paralelismo e ordenacao por partition.
- `offset`: posicao sequencial de uma mensagem dentro de uma partition.
- `consumer`: processo que le mensagens de um topic. Exemplo: `Payment.Service`.
- `consumer group`: grupo logico de consumidores que compartilham o processamento. Exemplos: `payment-service`, `order-service-payment-events`, `audit-worker`.
- `consumer lag`: diferenca entre o ultimo offset produzido e o ultimo offset processado por um consumer group.

## Kafka no Docker Compose Completo

Subir o ambiente completo:

```powershell
docker compose up -d --build
```

Listar topicos:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list
```

Consumir mensagens no ambiente completo:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:29092 --topic order-created --from-beginning --property print.key=true --property key.separator=" | "
```

Descrever consumer groups:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:29092 --describe --group payment-service
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:29092 --describe --group order-service-payment-events
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:29092 --describe --group audit-worker
```

## Kafka Isolado

O Kafka isolado da FASE 4.1 continua disponivel para estudo/debug especifico.

## Subir e Parar Kafka Isolado

Subir Kafka local em modo KRaft:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
```

Parar Kafka:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml down
```

Parar e apagar o volume local:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml down -v
```

Use `down -v` somente quando quiser limpar mensagens, offsets e dados locais do broker.

## Inspecionar Broker

Verificar container:

```powershell
docker ps --filter "name=kafka"
```

Verificar health:

```powershell
docker inspect kafka --format "{{json .State.Health}}"
```

Listar topicos:

```powershell
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list
```

Criar topicos do projeto:

```powershell
.\infra\kafka\create-topics.ps1
```

Descrever um topico:

```powershell
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --describe --topic order-created
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --describe --topic payment-approved
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --describe --topic payment-rejected
```

## Consumir Mensagens

Ler `order-created` desde o inicio:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic order-created --from-beginning --property print.key=true --property key.separator=" | "
```

Ler `payment-approved` desde o inicio:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic payment-approved --from-beginning --property print.key=true --property key.separator=" | "
```

Ler `payment-rejected` desde o inicio:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic payment-rejected --from-beginning --property print.key=true --property key.separator=" | "
```

Acompanhar somente mensagens novas:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic order-created --property print.key=true --property key.separator=" | "
```

Para inspecao, esse console consumer nao interfere no `Payment.Service` quando ele nao usa o mesmo consumer group. Sem `--group`, o console consumer cria uma leitura simples para aquele processo. Se voce passar `--group payment-service`, ai ele passa a competir com o `Payment.Service` pelo mesmo grupo.

## From Beginning vs Mensagens Novas

Com `--from-beginning`, o consumer le mensagens antigas ainda retidas no topico. Isso e otimo para estudar eventos ja publicados e repetir validacoes.

Sem `--from-beginning`, o consumer fica olhando para frente e mostra somente mensagens novas produzidas depois que ele iniciou.

## Mostrar Key, Partition e Offset

Mostrar key:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic order-created --from-beginning --property print.key=true --property key.separator=" | "
```

O console consumer mostra key e payload de forma simples. Para partition e offset, use logs dos servicos ou descreva consumer groups. O `Payment.Service` registra partition/offset ao consumir `OrderCreated`, e o `Order.Service` registra partition/offset ao consumir eventos de pagamento.

## Consumer Groups, Offsets e Lag

Listar consumer groups:

```powershell
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --list
```

Descrever o grupo do `Payment.Service`:

```powershell
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group payment-service
```

Descrever o grupo do `Order.Service` para eventos de pagamento:

```powershell
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group order-service-payment-events
```

Descrever o grupo do `Audit.Worker`:

```powershell
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group audit-worker
```

Na saida do `--describe`, observe:

- `CURRENT-OFFSET`: ultimo offset processado pelo grupo.
- `LOG-END-OFFSET`: offset mais recente disponivel no broker.
- `LAG`: mensagens pendentes para o grupo processar.
- `CONSUMER-ID`, `HOST` e `CLIENT-ID`: qual processo esta participando do grupo.

## Acompanhando o Fluxo em Tempo Real

Abra tres terminais simultaneamente.

Terminal 1, acompanhar `order-created`:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic order-created --property print.key=true --property key.separator=" | "
```

Terminal 2, acompanhar `payment-approved`:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic payment-approved --property print.key=true --property key.separator=" | "
```

Terminal 3, acompanhar `payment-rejected`:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic payment-rejected --property print.key=true --property key.separator=" | "
```

Depois, execute o fluxo local:

```powershell
dotnet run --project .\src\services\Inventory.Service\Inventory.Service.csproj --launch-profile https
cd .\src\services\Payment.Service
go run .\cmd\payment-service
cd ..\..\..
dotnet run --project .\src\services\Order.Service\Order.Service.csproj --launch-profile http
```

Com os servicos rodando, crie um pedido:

```powershell
$body = @{ productId = "<PRODUCT_ID>"; quantity = 1 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $body
```

Visualmente, voce deve ver:

1. `OrderCreated` aparecer em `order-created`.
2. `Payment.Service` consumir o evento.
3. `PaymentApproved` ou `PaymentRejected` aparecer no topico de pagamento.
4. `Order.Service` consumir o evento de pagamento e atualizar o pedido.
5. `Audit.Worker` gravar os eventos em `audit_events`.

## Audit.Worker

Executar o worker:

```powershell
dotnet run --project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

Configurar um consumer group isolado para validacao:

```powershell
$env:Kafka__ConsumerGroup = "audit-worker-fase7-validation"
dotnet run --project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

Consultar eventos auditados no PostgreSQL:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d audit_db -c "SELECT event_id, event_type, topic, message_key, kafka_partition, kafka_offset FROM audit_events ORDER BY audited_at DESC LIMIT 10;"
```

## Comandos Rapidos

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
.\infra\kafka\create-topics.ps1
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --describe --topic order-created
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic order-created --from-beginning --property print.key=true --property key.separator=" | "
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --list
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group payment-service
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group order-service-payment-events
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group audit-worker
docker compose -f .\infra\kafka\docker-compose.kafka.yml down
```
