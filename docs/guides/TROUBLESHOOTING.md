# Troubleshooting

Guia rapido para diagnosticar problemas comuns do MicroservicesLab.

## Sequencia Geral

Docker Compose:

```powershell
docker compose ps
docker compose logs --tail=120 <service>
docker compose config --quiet
```

Kubernetes:

```powershell
kubectl get pods -n microservices-lab
kubectl describe pod <pod> -n microservices-lab
kubectl logs <pod> -n microservices-lab
kubectl logs <pod> -n microservices-lab --previous
kubectl get events -n microservices-lab --sort-by=.lastTimestamp
```

Use:

- `get pods` para ver estado geral;
- `describe pod` para eventos, probes, imagens e scheduling;
- `logs` para erro da aplicacao atual;
- `logs --previous` para CrashLoopBackOff;
- `get events` para linha do tempo do cluster.

## Docker Container Nao Sobe

```powershell
docker compose ps
docker compose logs --tail=160 <service>
```

Verifique:

- variaveis de ambiente;
- dependencias indisponiveis;
- portas ja ocupadas;
- imagem nao buildada.

## PostgreSQL Indisponivel

Compose:

```powershell
docker compose exec -T postgres pg_isready -U postgres -d postgres
```

Kubernetes:

```powershell
kubectl logs deployment/postgres -n microservices-lab
kubectl get pvc -n microservices-lab
```

Sintomas comuns:

- services .NET falham ao aplicar migrations;
- `/health/ready` falha;
- `Connection refused` ou timeout para `postgres:5432`.

## Kafka Indisponivel

Compose:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list
```

Kubernetes:

```powershell
kubectl logs deployment/kafka -n microservices-lab --tail=160
kubectl exec -n microservices-lab deployment/kafka -- /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list
```

Verifique:

- Pod/Container esta `Ready`;
- Service `kafka` existe;
- topicos da aplicacao existem;
- Job `kafka-init` completou.

## Kafka Topic Ausente

Sintoma:

```text
Unknown topic or partition
Subscribed topic not available
```

Compose:

```powershell
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list
```

Kubernetes:

```powershell
kubectl exec -n microservices-lab deployment/kafka -- /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list
kubectl get jobs -n microservices-lab
```

Recuperar no Kubernetes recriando o Job:

```powershell
kubectl delete job kafka-init -n microservices-lab
kubectl apply -k k8s
kubectl get pods -n microservices-lab -w
```

Causa da perda de topicos no incidente documentado: nao determinada.

## Inventory Indisponivel / HTTP 503

Sintoma:

```text
POST /orders -> 503
```

Verifique:

```powershell
Invoke-RestMethod http://localhost:5276/health/ready
docker compose logs --tail=160 order-service
docker compose logs --tail=160 inventory-service
```

No Kubernetes:

```powershell
kubectl logs deployment/order-service -n microservices-lab --tail=160
kubectl logs deployment/inventory-service -n microservices-lab --tail=160
kubectl get endpointslices -n microservices-lab -l kubernetes.io/service-name=inventory-service
```

Possiveis causas:

- `Inventory.Service` parado;
- gRPC indisponivel;
- circuit breaker aberto;
- readiness do Inventory falhando.

## gRPC Falhando

Verifique a configuracao real:

```text
Compose:     InventoryGrpc__Address=http://inventory-service:8081
Kubernetes:  InventoryGrpc__Address=http://inventory-service:8081
Manual host: appsettings usa localhost/porta de launch profile
```

Logs:

```powershell
docker compose logs --tail=120 order-service
kubectl logs deployment/order-service -n microservices-lab --tail=120
```

## Circuit Breaker Aberto

Sintoma:

```text
HTTP 503 rapido depois de falhas consecutivas no Inventory
```

Observe:

```powershell
docker compose logs --tail=160 order-service
```

Metricas:

```promql
inventory_dependency_failures_total
inventory_circuit_breaker_opens_total
orders_rejected_total
```

Aguarde a janela configurada antes de testar recuperacao:

```text
Resilience__Inventory__CircuitBreakerOpenSeconds=10
```

## Pod Pending

```powershell
kubectl describe pod <pod> -n microservices-lab
kubectl get events -n microservices-lab --sort-by=.lastTimestamp
kubectl get pvc -n microservices-lab
```

Procure por falta de volume, imagem, recursos ou erro de scheduling.

## CrashLoopBackOff

```powershell
kubectl logs <pod> -n microservices-lab --previous
kubectl describe pod <pod> -n microservices-lab
```

No projeto, causas comuns:

- PostgreSQL ainda nao pronto;
- Kafka/topicos indisponiveis;
- connection string incorreta;
- erro de migration.

## Readiness Falhando

```powershell
kubectl describe pod <pod> -n microservices-lab
kubectl logs <pod> -n microservices-lab
```

Para servicos HTTP, readiness usa:

```text
/health/ready
```

Para Kafka, a probe usa TCP na porta `29092`.

## ImagePullBackOff

Verifique nome/tag:

```powershell
docker image ls
kubectl describe pod <pod> -n microservices-lab
```

No Docker Desktop Kubernetes, as imagens locais esperadas sao:

```text
microservices-lab-inventory-service:latest
microservices-lab-order-service:latest
microservices-lab-payment-service:latest
microservices-lab-audit-worker:latest
```

Rebuild:

```powershell
docker compose build inventory-service order-service payment-service audit-worker
```

## Job Completed

`Completed` e esperado para `kafka-init`.

```text
Job
  -> executa tarefa finita
  -> cria topicos
  -> termina
```

Ele nao monitora continuamente se os topicos continuam existindo. Se precisar recriar:

```powershell
kubectl delete job kafka-init -n microservices-lab
kubectl apply -k k8s
```

## Segredos Locais

As Secrets versionadas contem credenciais locais de laboratorio, como `postgres/postgres` e `admin/admin`.

```text
base64 != criptografia forte
```

Nao use esses valores em ambiente real.
