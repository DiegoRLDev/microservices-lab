# Executando Localmente

Este guia e o caminho rapido para executar o ambiente atual do MicroservicesLab.

Guias relacionados:

- [Quick Reference](QUICK-REFERENCE.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Labs](../labs/README.md)

## Execucao rapida com Docker Compose

Esta e a forma recomendada a partir da FASE 8. Os comandos assumem PowerShell aberto na raiz do repositorio `MicroservicesLab`.

Subir tudo com rebuild:

```powershell
docker compose up -d --build
```

Rebuildar e subir novamente:

```powershell
docker compose up -d --build
```

Ver status:

```powershell
docker compose ps
```

Ver todos os logs:

```powershell
docker compose logs
```

Acompanhar logs:

```powershell
docker compose logs -f
```

Acompanhar logs de um servico:

```powershell
docker compose logs -f order-service
docker compose logs -f payment-service
docker compose logs -f audit-worker
```

Rodar labs de resiliencia da FASE 9:

```text
docs/guides/RESILIENCE.md
```

Rodar labs de observabilidade da FASE 10:

```text
docs/guides/OBSERVABILITY.md
```

Rodar o laboratorio Kubernetes da FASE 11:

```text
docs/guides/KUBERNETES.md
```

Parar tudo sem apagar volumes:

```powershell
docker compose down
```

Reiniciar um servico:

```powershell
docker compose restart order-service
```

Rebuildar somente um servico:

```powershell
docker compose up -d --build order-service
```

Limpar dados locais intencionalmente:

```powershell
docker compose down -v
```

`docker compose down` para os containers, mas preserva os volumes nomeados. `docker compose down -v` remove os volumes `postgres_data` e `kafka_data`, apagando bancos, mensagens e offsets locais.

## Execucao com Kubernetes

O Docker Compose continua sendo o modo mais simples para rodar localmente. Kubernetes esta disponivel para estudar Pods, Deployments, Services, ConfigMaps, Secrets, probes, replicas, self-healing e rolling update.

Buildar imagens locais:

```powershell
docker compose build inventory-service order-service payment-service audit-worker
```

Aplicar manifests:

```powershell
kubectl apply -k k8s
```

Verificar:

```powershell
kubectl get pods -n microservices-lab
kubectl get services -n microservices-lab
```

Acessar APIs:

```powershell
kubectl port-forward service/order-service 15074:8080 -n microservices-lab
kubectl port-forward service/inventory-service 15276:8080 -n microservices-lab
```

Remover o ambiente Kubernetes:

```powershell
kubectl delete namespace microservices-lab
```

Resumo do ambiente Docker Compose:

| Componente | Tecnologia | Porta/endpoint | Dependencia |
| --- | --- | --- | --- |
| PostgreSQL | PostgreSQL | `localhost:5432` | - |
| Kafka | Kafka `apache/kafka:3.9.0` | host `localhost:9092`; containers `kafka:29092` | - |
| Inventory.Service | .NET 9 | HTTP `http://localhost:5276`; gRPC `http://inventory-service:8081` no Docker; host `localhost:7242` mapeado para inspecao | PostgreSQL |
| Payment.Service | Go | sem HTTP | Kafka |
| Order.Service | .NET 9 | HTTP `http://localhost:5074` | PostgreSQL, Inventory.Service gRPC, Kafka |
| Audit.Worker | .NET 9 Worker | sem HTTP | Kafka, PostgreSQL |
| OpenTelemetry Collector | OTel Collector | OTLP gRPC `localhost:4317`, OTLP HTTP `localhost:4318`, Prometheus exporter `localhost:9464` | - |
| Jaeger | Jaeger all-in-one | `http://localhost:16686` | OpenTelemetry Collector |
| Prometheus | Prometheus | `http://localhost:9090` | OpenTelemetry Collector |
| Grafana | Grafana | `http://localhost:3000` | Prometheus |

### Verificar se o ambiente esta funcionando

```powershell
# Containers ativos de infraestrutura.
docker compose ps

# Kafka saudavel e topicos ativos.
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list

# PostgreSQL e bancos usados pelos servicos.
Test-NetConnection localhost -Port 5432
docker compose exec postgres psql -U postgres -d postgres -c "SELECT datname FROM pg_database WHERE datname IN ('order_db','inventory_db','audit_db') ORDER BY datname;"

# Portas HTTP dos servicos.
Test-NetConnection localhost -Port 5276
Test-NetConnection localhost -Port 5074

# Health dos servicos HTTP. Se algum comando falhar, confirme se o terminal do servico correspondente esta rodando.
Invoke-RestMethod http://localhost:5276/health/ready
Invoke-RestMethod http://localhost:5074/health/ready

# Servicos sem endpoint HTTP.
docker compose ps payment-service audit-worker
docker compose logs --tail=40 payment-service audit-worker
```

## Execucao manual

A execucao manual continua disponivel como alternativa didatica e para debug fino dos servicos. Use as secoes detalhadas abaixo quando quiser estudar cada componente separadamente.

### Ordem rapida manual

Terminal 1 - Infraestrutura:

```powershell
# PostgreSQL usado nas validacoes manuais anteriores.
docker start microserviceslab-postgres
docker ps --filter "name=microserviceslab-postgres"
Test-NetConnection localhost -Port 5432

# Kafka isolado da FASE 4.1.
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
powershell -ExecutionPolicy Bypass -File .\infra\kafka\create-topics.ps1
```

Terminal 2 - Inventory.Service:

```powershell
dotnet run --project .\src\services\Inventory.Service\Inventory.Service.csproj --launch-profile https
```

Terminal 3 - Payment.Service:

```powershell
cd .\src\services\Payment.Service
go run .\cmd\payment-service
```

Se estiver usando Git Bash, use barras `/`:

```bash
cd ./src/services/Payment.Service
go run ./cmd/payment-service
```

Se o terminal ja estiver em `src/services/Payment.Service`, execute apenas:

```bash
go run ./cmd/payment-service
```

Terminal 4 - Order.Service:

```powershell
dotnet run --project .\src\services\Order.Service\Order.Service.csproj --launch-profile http
```

Terminal 5 - Audit.Worker:

```powershell
dotnet run --project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

## Ambiente Atual

```text
Cliente
  |
  v
Order.Service (.NET)
  |
  +--- gRPC ---> Inventory.Service (.NET)
  |                  |
  |                  v
  |             inventory_db
  |
  +--- order_db
  |
  +--- OrderCreated ---> Kafka
                         |
                         v
                  Payment.Service (Go)
                         |
             +-----------+-----------+
             |                       |
             v                       v
      PaymentApproved          PaymentRejected
             |                       |
             +-----------+-----------+
                         |
                         v
                  Order.Service
                         |
                         v
                  Audit.Worker
                         |
                         v
                     audit_db
```

## Ordem Recomendada

1. PostgreSQL.
2. Kafka.
3. `Inventory.Service`.
4. `Payment.Service`.
5. `Audit.Worker`.
6. `Order.Service`.

`Order.Service` pode subir antes dos consumidores, mas para observar o fluxo completo em tempo real e mais facil deixar `Payment.Service` e `Audit.Worker` rodando antes de criar pedidos.

## Portas

```text
PostgreSQL: 5432
Kafka: 9092
Inventory.Service HTTP: 5276
Inventory.Service HTTPS/gRPC: 7242
Order.Service HTTP: 5074
Order.Service HTTPS: 7213
Audit.Worker: sem porta HTTP
Jaeger: 16686
Prometheus: 9090
Grafana: 3000
OTLP gRPC: 4317
OTLP HTTP: 4318
Collector metrics: 9464
```

## Bancos

```text
Order.Service     -> order_db
Inventory.Service -> inventory_db
Audit.Worker      -> audit_db
```

## Topicos Kafka

```text
order-created
payment-approved
payment-rejected
```

## 1. PostgreSQL

Verificar acesso:

```powershell
Test-NetConnection localhost -Port 5432
```

Listar bancos do laboratorio:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d postgres -c "SELECT datname FROM pg_database WHERE datname IN ('order_db','inventory_db','audit_db') ORDER BY datname;"
```

Aplicar migrations, se necessario:

```powershell
dotnet ef database update --project .\src\services\Order.Service\Order.Service.csproj --startup-project .\src\services\Order.Service\Order.Service.csproj
dotnet ef database update --project .\src\services\Inventory.Service\Inventory.Service.csproj --startup-project .\src\services\Inventory.Service\Inventory.Service.csproj
dotnet ef database update --project .\src\workers\Audit.Worker\Audit.Worker.csproj --startup-project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

## 2. Kafka

Subir Kafka:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
```

Criar topicos:

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\kafka\create-topics.ps1
```

Verificar:

```powershell
docker inspect kafka --format "{{json .State.Health}}"
docker exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list
```

## 3. Inventory.Service

Terminal 1:

```powershell
dotnet run --project .\src\services\Inventory.Service\Inventory.Service.csproj --launch-profile https
```

Health:

```powershell
Invoke-RestMethod http://localhost:5276/health/ready
```

Listar produtos:

```powershell
Invoke-RestMethod http://localhost:5276/products | ConvertTo-Json -Depth 10
```

Criar produto:

```powershell
$body = @{ name = "Produto Local"; price = 100.00; availableQuantity = 10 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5276/products -ContentType "application/json" -Body $body
```

## 4. Payment.Service

Terminal 2:

```powershell
cd .\src\services\Payment.Service
go run .\cmd\payment-service
```

No Git Bash, a partir da raiz do repositorio:

```bash
cd ./src/services/Payment.Service
go run ./cmd/payment-service
```

No Git Bash, se ja estiver em `src/services/Payment.Service`:

```bash
go run ./cmd/payment-service
```

Variaveis opcionais:

```powershell
$env:KAFKA_GROUP_ID = "payment-service"
$env:PAYMENT_MAX_AUTO_APPROVAL_AMOUNT = "10000"
```

## 5. Audit.Worker

Terminal 3:

```powershell
dotnet run --project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

## 6. Order.Service

Terminal 4:

```powershell
dotnet run --project .\src\services\Order.Service\Order.Service.csproj --launch-profile http
```

Health:

```powershell
Invoke-RestMethod http://localhost:5074/health/ready
```

## Criar Pedido

Use um `ProductId` existente do `Inventory.Service`:

```powershell
$body = @{ productId = "<PRODUCT_ID>"; quantity = 1 } | ConvertTo-Json
$order = Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $body
$order | ConvertTo-Json -Depth 10
```

Consultar depois:

```powershell
Invoke-RestMethod "http://localhost:5074/orders/$($order.id)" | ConvertTo-Json -Depth 10
```

Consultar no banco:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d order_db -c "SELECT id, product_id, quantity, unit_price, total_price, status, created_at FROM orders ORDER BY created_at DESC LIMIT 5;"
```

Consultar auditoria:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d audit_db -c "SELECT event_id, event_type, topic, message_key, kafka_partition, kafka_offset FROM audit_events ORDER BY audited_at DESC LIMIT 10;"
```

## Observar Eventos Kafka

Em terminais separados:

```powershell
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic order-created --property print.key=true --property key.separator=" | "
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic payment-approved --property print.key=true --property key.separator=" | "
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic payment-rejected --property print.key=true --property key.separator=" | "
```

Para ler tambem mensagens antigas, adicione `--from-beginning`.

## Validar Codigo

Da raiz do repositorio:

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
```

Modulo Go:

```powershell
cd .\src\services\Payment.Service
go test ./...
go build .\cmd\payment-service
Remove-Item -LiteralPath .\payment-service.exe -ErrorAction SilentlyContinue
```
