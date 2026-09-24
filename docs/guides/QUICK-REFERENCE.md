# Quick Reference

Comandos curtos para consulta rapida no MicroservicesLab.

## Docker Compose

```powershell
docker compose up -d --build
docker compose ps
docker compose logs --tail=100 order-service
docker compose logs -f payment-service
docker compose down
docker compose down -v
docker compose config --quiet
```

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
