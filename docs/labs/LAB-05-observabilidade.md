# LAB 05 - Observabilidade

## Objetivo

Usar Grafana, Prometheus e Jaeger para observar o fluxo principal.

## Pre-Requisitos

Docker Compose rodando:

```powershell
docker compose up -d --build
```

Validar UIs:

```powershell
Invoke-RestMethod http://localhost:9090/-/ready
Invoke-RestMethod http://localhost:3000/api/health
```

## Passos

Gerar trafego seguindo o [LAB 01](LAB-01-happy-path.md).

Abrir Grafana:

```text
http://localhost:3000/d/microserviceslab/microserviceslab
```

Credenciais:

```text
admin / admin
```

Consultar Prometheus:

```text
http://localhost:9090
```

PromQL uteis:

```promql
orders_created_total
payments_approved_total
orders_rejected_total
inventory_dependency_failures_total
inventory_circuit_breaker_opens_total
http_server_request_duration_seconds_count{http_response_status_code="503"}
```

Abrir Jaeger:

```text
http://localhost:16686
```

Buscar por:

```text
MicroservicesLab.OrderService
```

## O Que Observar

Fluxo mental:

```text
Grafana
"Existe um problema?"
        ↓
Prometheus
"Quanto esta acontecendo?"
        ↓
Jaeger
"O que aconteceu nesta requisicao?"
```

## Resultado Esperado

- Grafana mostra contadores e requisicoes.
- Prometheus retorna metricas reais.
- Jaeger mostra spans de HTTP, gRPC, EF Core, Kafka produce/consume e Payment.

## Limpeza

Nao ha limpeza obrigatoria.

## Conceito Demonstrado

Observabilidade por metricas, traces e logs correlacionados.
