# Quick Reference

Comandos curtos para consulta rapida no MicroservicesLab.

## Docker Compose

```powershell
docker compose up -d --build
docker compose ps --all
docker compose logs --tail=100 order-service
docker compose logs -f payment-service
docker compose down
docker compose config --quiet
```

kafka-init Exited (0) e esperado. Nao use down -v/prune para troubleshooting: removem dados persistidos. Guia completo: [execucao local](RUNNING-LOCALLY.md).

## Angular e APIs

Angular: http://localhost:4200. APIs no host: Order 5074, Inventory 5276, Audit 5288, PriceFeed 5080, Pricing 5090. Payment nao possui HTTP.

```powershell
Push-Location .\src\frontend\MicroservicesLab.App
pnpm install --frozen-lockfile
pnpm test:ci
pnpm build
Pop-Location
Invoke-RestMethod http://localhost:4200/api/audit/health
Invoke-RestMethod 'http://localhost:4200/api/audit/audit-events?limit=5'
Invoke-RestMethod http://localhost:4200/api/pricing/prices
curl.exe -N --max-time 3 http://localhost:4200/api/pricefeed/prices/stream
```

O stream termina por timeout intencional (curl exit 28). Desenvolvimento usa proxy.conf.json; use porta 4201 se frontend Docker ocupar 4200. Testes integrados opcionais: [procedimentos e cuidados](../INTEGRATION_TESTS.md).

## .NET

```powershell
dotnet build .\MicroservicesLab.sln
dotnet test .\MicroservicesLab.sln --no-build
dotnet run --project .\src\services\Order.Service\Order.Service.csproj --launch-profile http
dotnet run --project .\src\services\Inventory.Service\Inventory.Service.csproj --launch-profile https
dotnet run --project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

## Go

```powershell
cd .\src\services\Payment.Service
go test ./...
go run .\cmd\payment-service
cd ..\..\..
```

PriceFeed, a partir da raiz:

```powershell
Push-Location .\src\services\PriceFeed.Service
go test ./...
Pop-Location
```

Para executar localmente sem conflito com Prometheus em 9090, configure PRICE_HTTP_ADDR=:5080 e PRICE_GRPC_ADDR=:19090. Nao execute a copia local junto com a copia Docker: [guia local](RUNNING-LOCALLY.md).

## PostgreSQL

```powershell
docker compose exec -T postgres psql -U postgres -d postgres -c "SELECT datname FROM pg_database ORDER BY datname;"
docker compose exec -T postgres psql -U postgres -d order_db -c "SELECT id, status, created_at FROM orders ORDER BY created_at DESC LIMIT 5;"
docker compose exec -T postgres psql -U postgres -d audit_db -c "SELECT event_type, topic, message_key, kafka_partition, kafka_offset FROM audit_events ORDER BY audited_at DESC LIMIT 10;"
```

## Kafka

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:29092 --topic order-created --from-beginning --property print.key=true --property key.separator=" | "
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:29092 --topic payment-approved --from-beginning --property print.key=true --property key.separator=" | "
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:29092 --topic payment-rejected --from-beginning --property print.key=true --property key.separator=" | "
```

## Jaeger

```text
Docker Compose: http://localhost:16686
Kubernetes:     kubectl port-forward service/jaeger 16687:16686 -n microservices-lab
```

## Prometheus

```text
Docker Compose: http://localhost:9090
Kubernetes:     kubectl port-forward service/prometheus 19090:9090 -n microservices-lab
```

PromQL:

```promql
orders_created_total
payments_approved_total
orders_rejected_total
inventory_dependency_failures_total
inventory_circuit_breaker_opens_total
http_server_request_duration_seconds_count
```

## Grafana

```text
Docker Compose: http://localhost:3000/d/microserviceslab/microserviceslab
Kubernetes:     kubectl port-forward service/grafana 13000:3000 -n microservices-lab
Login:          admin / admin
```

## Kubernetes

```powershell
kubectl get pods -n microservices-lab
kubectl get deployments -n microservices-lab
kubectl get services -n microservices-lab
kubectl get jobs -n microservices-lab
kubectl get configmaps -n microservices-lab
kubectl get secrets -n microservices-lab
kubectl get events -n microservices-lab --sort-by=.lastTimestamp
```

Diagnostico:

```powershell
kubectl logs <pod> -n microservices-lab
kubectl logs <pod> -n microservices-lab --previous
kubectl describe pod <pod> -n microservices-lab
```

Scaling e rollout:

```powershell
kubectl scale deployment/inventory-service --replicas=3 -n microservices-lab
kubectl rollout status deployment/inventory-service -n microservices-lab
kubectl rollout history deployment/inventory-service -n microservices-lab
kubectl get endpointslices -n microservices-lab -l kubernetes.io/service-name=inventory-service
```

Aplicar/remover ambiente:

```powershell
docker compose build inventory-service order-service payment-service audit-worker
kubectl apply -k k8s
kubectl delete namespace microservices-lab
```
