# LAB 15 - Kafka PriceUpdated

Este lab valida a FASE 15:

```text
PriceFeed.Service -> Kafka -> price-updated
```

O objetivo e observar producer, topic, key, partitions, ordering por partition e desacoplamento temporal. `Pricing.Service` ainda nao consome Kafka nesta fase.

## Preparacao

Subir Kafka e criar topicos:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
.\infra\kafka\create-topics.ps1
```

Alternativa se scripts PowerShell estiverem bloqueados:

```powershell
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --create --if-not-exists --topic price-updated --partitions 3 --replication-factor 1
```

Confirmar topicos:

```powershell
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list
```

## LAB 15.A - Producer

Terminal 1:

```powershell
cd .\src\services\PriceFeed.Service
$env:PRICE_KAFKA_ENABLED = "true"
$env:PRICE_KAFKA_BOOTSTRAP_SERVERS = "localhost:9092"
$env:PRICE_KAFKA_TOPIC = "price-updated"
go run .
```

Terminal 2:

```powershell
docker exec -it kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic price-updated --from-beginning --property print.key=true --property key.separator=" | "
```

Saida esperada:

```text
PETR4 | {"symbol":"PETR4","price":3815,"occurredAt":"2026-09-29T14:30:00Z"}
VALE3 | {"symbol":"VALE3","price":7421,"occurredAt":"2026-09-29T14:30:00Z"}
```

## LAB 15.B - Key e Partitions

Descrever o topico:

```powershell
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --describe --topic price-updated
```

Consumir mostrando key e metadados possiveis:

```powershell
docker exec -it kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic price-updated --from-beginning --property print.key=true --property key.separator=" | " --property print.partition=true --property print.offset=true
```

O ponto de observacao e que `key=symbol`. Com hash partitioner, a mesma key tende a ser direcionada consistentemente para a mesma partition. Kafka preserva ordering dentro de uma partition, mas nao ordering global entre todas as partitions.

## LAB 15.C - Desacoplamento Temporal

Terminal 1: mantenha o producer rodando.

Terminal 2: consuma por alguns segundos e pare o consumidor com `Ctrl+C`:

```powershell
docker exec -it kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic price-updated --group price-lab-retention --property print.key=true --property key.separator=" | "
```

Aguarde alguns segundos com o `PriceFeed.Service` ainda produzindo.

Reabra o consumidor com o mesmo group:

```powershell
docker exec -it kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic price-updated --group price-lab-retention --property print.key=true --property key.separator=" | "
```

Comparacao didatica:

```text
SSE/gRPC desconectado -> cliente nao recebe eventos daquele periodo
Kafka consumer parado -> broker continua recebendo eventos; consumidor pode retomar eventos retidos conforme offset e retention
```

## LAB 15.D - Frequencia

Executar com frequencia padrao:

```powershell
$env:PRICE_UPDATE_INTERVAL = "500ms"
go run .
```

Executar novamente com frequencia maior:

```powershell
$env:PRICE_UPDATE_INTERVAL = "100ms"
go run .
```

Com 5 simbolos, a ordem de grandeza esperada e:

```text
500ms -> aproximadamente 10 eventos/s
100ms -> aproximadamente 50 eventos/s
```

Nao e benchmark formal. A FASE 17 aprofundara backpressure.
