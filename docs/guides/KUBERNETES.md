# Kubernetes no MicroservicesLab

Este guia registra a FASE 11. O objetivo e executar o mesmo laboratorio que ja funciona em Docker Compose dentro de um cluster Kubernetes local.

## Docker Compose x Kubernetes

Docker Compose sobe containers definidos em um arquivo. Kubernetes trabalha com estado desejado: voce declara Deployments, Services, ConfigMaps, Secrets, PVCs e probes, e o control plane tenta manter esse estado mesmo quando Pods falham ou sao substituidos.

No MicroservicesLab:

```text
Docker Compose
  -> caminho simples/local
  -> docker compose up -d --build

Kubernetes
  -> caminho para estudar Pods, Services, replicas, probes, self-healing e rolling update
  -> kubectl apply -k k8s
```

Os dois modos continuam validos.

## Conceitos Usados

### Pod

Unidade executavel do Kubernetes. Cada instancia de `Order.Service`, `Inventory.Service`, `Payment.Service`, `Audit.Worker`, PostgreSQL, Kafka e observabilidade roda em um Pod.

### Deployment

Declara quantas replicas de um Pod devem existir. Se um Pod morre, o Deployment cria outro. Exemplo: `inventory-service` com `replicas=2`.

### Service

Nome DNS estavel para um conjunto de Pods. `Order.Service` chama `inventory-service:8081`, sem conhecer IP de Pod.

### ClusterIP

Service interno do cluster. Usado para `postgres`, `kafka`, `inventory-service`, `order-service`, `otel-collector`, `jaeger`, `prometheus` e `grafana`.

### ConfigMap

Configuracao nao sensivel, como bootstrap Kafka, endpoint OTLP, nomes de topicos e endpoints internos.

### Secret

Configuracao sensivel ou didaticamente sensivel, como senha local do PostgreSQL e senha admin do Grafana. Secret em Kubernetes usa base64, mas isso nao e criptografia forte por si so.

### Probe

`livenessProbe` responde se o processo deve continuar vivo. `readinessProbe` responde se o Pod pode receber trafego pelo Service.

### Replica

Instancia desejada de um Pod. Quando `inventory-service` tem `replicas=2`, o Deployment mantem dois Pods prontos.

### Desired State

Estado declarado. Se `replicas=2` e um Pod e apagado, o estado real vira 1, e Kubernetes cria outro Pod para voltar ao estado desejado.

## Arquitetura

```text
                        Kubernetes Cluster
┌─────────────────────────────────────────────────────────┐
│ Client                                                  │
│   │                                                     │
│   ▼                                                     │
│ order-service (Service)                                 │
│   │                                                     │
│   ▼                                                     │
│ Order Pod                                               │
│   │                                                     │
│   ├── gRPC ──> inventory-service                        │
│   │               ├── Inventory Pod A                   │
│   │               └── Inventory Pod B                   │
│   │                                                     │
│   └── Kafka ──> kafka                                   │
│                    ├── Payment Pod                      │
│                    └── Audit Pod                        │
│                                                         │
│ postgres                                                │
│ otel-collector -> jaeger                                │
│ otel-collector -> prometheus -> grafana                 │
└─────────────────────────────────────────────────────────┘
```

## Pre-Requisitos

Validado com Kubernetes local do Docker Desktop:

```powershell
kubectl version --client
kubectl cluster-info
kubectl get nodes
```

Cluster usado na validacao:

```text
desktop-control-plane
containerd
Docker Desktop Kubernetes
```

## Build das Imagens

As mesmas Dockerfiles do Compose sao reutilizadas:

```powershell
docker compose build inventory-service order-service payment-service audit-worker
```

Ou:

```powershell
.\scripts\k8s\build-images.ps1
```

No Docker Desktop Kubernetes, as imagens locais foram usadas com `imagePullPolicy: IfNotPresent`.

## Aplicando Manifests

```powershell
kubectl apply -k k8s
```

Ou:

```powershell
.\scripts\k8s\apply.ps1
```

Namespace:

```text
microservices-lab
```

## Verificando Pods

```powershell
kubectl get pods -n microservices-lab
kubectl get deployments -n microservices-lab
kubectl get services -n microservices-lab
kubectl get jobs -n microservices-lab
```

`kafka-init` aparecer como `Completed` e esperado: ele cria os topicos e encerra.

## Services e Service Discovery

`Order.Service` usa:

```text
InventoryGrpc__Address=http://inventory-service:8081
Kafka__BootstrapServers=kafka:29092
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

Ele conhece nomes de Services, nao IPs de Pods. O DNS interno do Kubernetes resolve `inventory-service`, `kafka`, `postgres` e `otel-collector`.

## Acessando APIs Pelo Host

Use port-forward:

```powershell
kubectl port-forward service/order-service 15074:8080 -n microservices-lab
kubectl port-forward service/inventory-service 15276:8080 -n microservices-lab
```

URLs:

```text
Order: http://localhost:15074
Inventory: http://localhost:15276
```

## Liveness e Readiness

Servicos HTTP usam endpoints existentes:

```text
/health/live
/health/ready
```

Workers (`Payment.Service` e `Audit.Worker`) nao expõem HTTP artificial apenas para probes. A decisao e proporcional: eles usam initContainers para aguardar Kafka/PostgreSQL e depois rodam como consumidores.

Kafka usa TCP probe em `29092`. A tentativa inicial com `kafka-topics.sh` na readiness criou um ciclo, porque o client recebia metadata `kafka:29092`, mas o Service ainda nao tinha endpoint antes da readiness passar.

## Escalando Inventory

```powershell
kubectl scale deployment/inventory-service --replicas=2 -n microservices-lab
kubectl rollout status deployment/inventory-service -n microservices-lab
kubectl get pods -l app=inventory-service -n microservices-lab -o wide
```

Evidencia real:

```text
inventory-service-f98c66d6f-kwbw7   Running
inventory-service-f98c66d6f-x867c   Running
```

## Laboratorio Self-Healing

Apague um Pod:

```powershell
kubectl delete pod <inventory-pod> -n microservices-lab
kubectl get pods -l app=inventory-service -n microservices-lab -w
```

Evidencia real:

```text
Pod apagado:
inventory-service-f98c66d6f-kwbw7

Novo Pod criado automaticamente:
inventory-service-f98c66d6f-r9sl9
```

Explicacao aplicada:

```text
desired state: replicas = 2
actual state apos delete: replicas = 1
Kubernetes cria novo Pod para voltar a 2
```

Depois da perda do Pod, o Service continuou funcional e um novo pedido terminou `Paid`:

```text
OrderId: c821b792-0690-4e64-8c40-4076c6f5220d
Status: Paid
```

## Laboratorio Rolling Update

Criar nova tag local:

```powershell
docker tag microservices-lab-inventory-service:latest microservices-lab-inventory-service:phase11-v2
```

Atualizar imagem:

```powershell
kubectl set image deployment/inventory-service inventory-service=microservices-lab-inventory-service:phase11-v2 -n microservices-lab
kubectl rollout status deployment/inventory-service -n microservices-lab
kubectl rollout history deployment/inventory-service -n microservices-lab
```

Evidencia real:

```text
Durante:
novo Pod Running ainda nao Ready
Pod antigo ainda Ready

Depois:
novos Pods Ready
Pod antigo Terminating
```

Validacao pos-rollout:

```text
OrderId: 36ca1331-3790-490d-9090-558d7a212262
Status: Paid
```

## Validacao E2E

Criar produto:

```powershell
$productBody = @{
  name = "Produto Kubernetes Fase 11"
  price = 190.00
  availableQuantity = 5
} | ConvertTo-Json

$product = Invoke-RestMethod -Method Post -Uri http://localhost:15276/products -ContentType "application/json" -Body $productBody
```

Criar pedido:

```powershell
$orderBody = @{
  productId = $product.id
  quantity = 1
} | ConvertTo-Json

$order = Invoke-RestMethod -Method Post -Uri http://localhost:15074/orders -ContentType "application/json" -Body $orderBody
Start-Sleep -Seconds 8
Invoke-RestMethod "http://localhost:15074/orders/$($order.id)"
```

Evidencia real:

```text
Produto: c17b69f1-2066-49d2-b90e-28500ad99a48
Pedido: 4f3139b6-8297-42fb-b0db-a6b02a6618bc
Status final: Paid
TraceId: 31b5798a07ededf806c732e1daecc1b8
```

Auditoria:

```text
OrderCreated    order-created     key 4f3139b6-8297-42fb-b0db-a6b02a6618bc
PaymentApproved payment-approved  key 4f3139b6-8297-42fb-b0db-a6b02a6618bc
```

## Observabilidade

Port-forward:

```powershell
kubectl port-forward service/jaeger 16687:16686 -n microservices-lab
kubectl port-forward service/prometheus 19090:9090 -n microservices-lab
kubectl port-forward service/grafana 13000:3000 -n microservices-lab
```

URLs:

```text
Jaeger: http://localhost:16687
Prometheus: http://localhost:19090
Grafana: http://localhost:13000
```

O trace real `31b5798a07ededf806c732e1daecc1b8` mostrou:

```text
OrderService POST /orders
InventoryService CheckAvailability
Kafka produce order-created
PaymentService consume/process/produce
OrderService consume payment-approved
AuditWorker audit order-created/payment-approved
```

Metricas confirmadas:

```text
orders_created_total = 1
payments_approved_total = 1
http_server_request_duration_seconds_count com POST /orders e gRPC Inventory
```

Grafana provisionou:

```text
/d/microserviceslab/microserviceslab
```

## Logs e Diagnostico

Comandos uteis:

```powershell
kubectl describe pod <pod> -n microservices-lab
kubectl logs <pod> -n microservices-lab
kubectl logs deployment/order-service -n microservices-lab --tail=100
kubectl get events -n microservices-lab --sort-by=.lastTimestamp
```

Diagnostico rapido:

```text
Pod Pending:
  kubectl describe pod
  kubectl get events

ImagePullBackOff:
  confirme nome/tag da imagem e estrategia local

CrashLoopBackOff:
  kubectl logs --previous

Readiness failing:
  verifique /health/ready, dependencias e Service DNS
```

Problemas reais encontrados nesta fase:

```text
Problema: apps .NET iniciavam antes do PostgreSQL aceitar conexao.
Causa: Kubernetes nao tem depends_on como Compose.
Solucao: initContainers aguardando PostgreSQL/Kafka/Inventory.

Problema: readiness do Kafka com kafka-topics.sh causava impasse.
Causa: client recebia metadata kafka:29092 antes do Service ter endpoints Ready.
Solucao: readiness/liveness TCP socket em 29092; kafka-init Job valida topicos.

Problema: port-forward do Inventory caiu durante rolling update.
Causa: o tunel estava preso a um Pod antigo encerrado pelo rollout.
Solucao: reabrir port-forward para o Service.
```

## Derrubando o Ambiente

Remove apenas o namespace do laboratorio:

```powershell
kubectl delete namespace microservices-lab
```

Ou:

```powershell
.\scripts\k8s\delete.ps1
```

## Roteiro Manual Final

```text
1. kubectl cluster-info
2. docker compose build inventory-service order-service payment-service audit-worker
3. kubectl apply -k k8s
4. kubectl get pods -n microservices-lab
5. kubectl get services -n microservices-lab
6. kubectl port-forward service/order-service 15074:8080 -n microservices-lab
7. kubectl port-forward service/inventory-service 15276:8080 -n microservices-lab
8. POST /products
9. POST /orders
10. GET /orders/{id} e confirmar Paid
11. kubectl scale deployment/inventory-service --replicas=2 -n microservices-lab
12. kubectl delete pod <inventory-pod> -n microservices-lab
13. kubectl get pods -l app=inventory-service -n microservices-lab -w
14. criar novo pedido e confirmar que continua Paid
15. docker tag microservices-lab-inventory-service:latest microservices-lab-inventory-service:phase11-v2
16. kubectl set image deployment/inventory-service inventory-service=microservices-lab-inventory-service:phase11-v2 -n microservices-lab
17. kubectl rollout status deployment/inventory-service -n microservices-lab
18. kubectl port-forward service/jaeger 16687:16686 -n microservices-lab
19. kubectl port-forward service/prometheus 19090:9090 -n microservices-lab
20. kubectl port-forward service/grafana 13000:3000 -n microservices-lab
```
