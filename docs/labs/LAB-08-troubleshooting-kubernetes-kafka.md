# LAB 08 - Troubleshooting Kubernetes/Kafka

## Objetivo

Documentar um incidente real observado durante validacao Kubernetes: servicos dependentes de Kafka falharam porque os topicos da aplicacao estavam ausentes.

## Sintoma

```text
audit-worker  -> CrashLoopBackOff
order-service -> CrashLoopBackOff
```

## Diagnostico Inicial

Identificar Pods:

```powershell
kubectl get pods -n microservices-lab
```

Ler logs anteriores:

```powershell
kubectl logs <order-pod> -n microservices-lab --previous
kubectl logs <audit-pod> -n microservices-lab --previous
```

Descrever Pod:

```powershell
kubectl describe pod <order-pod> -n microservices-lab
```

Erro observado:

```text
Subscribed topic not available:
payment-approved
Broker: Unknown topic or partition
```

## Verificacao do Kafka

Listar topicos:

```powershell
kubectl exec -n microservices-lab deployment/kafka -- `
  /opt/kafka/bin/kafka-topics.sh `
  --bootstrap-server localhost:29092 `
  --list
```

Estado encontrado:

```text
__consumer_offsets
```

Topicos ausentes:

```text
order-created
payment-approved
payment-rejected
```

O Job estava concluido:

```powershell
kubectl get jobs -n microservices-lab
```

```text
kafka-init   Complete / Completed
```

## Job x Deployment

```text
Deployment
  -> mantem estado desejado continuamente

Job
  -> executa uma tarefa ate sucesso
  -> depois fica Completed
  -> nao monitora continuamente o efeito produzido
```

## Recuperacao Realizada

Recriar o Job de inicializacao:

```powershell
kubectl delete job kafka-init -n microservices-lab
kubectl apply -k k8s
```

Acompanhar:

```powershell
kubectl get pods -n microservices-lab -w
```

Resultado observado:

```text
kafka-init -> Completed

Order.Service:
CrashLoopBackOff -> Running -> Ready

Audit.Worker:
CrashLoopBackOff -> Running -> Ready
```

Confirmar topicos:

```powershell
kubectl exec -n microservices-lab deployment/kafka -- `
  /opt/kafka/bin/kafka-topics.sh `
  --bootstrap-server localhost:29092 `
  --list
```

Resultado esperado:

```text
__consumer_offsets
order-created
payment-approved
payment-rejected
```

## Observacao Importante

Causa da perda dos topicos: nao determinada neste laboratorio.

Sabemos apenas que:

- o Job havia sido concluido anteriormente;
- no momento do diagnostico os topicos nao existiam;
- recriar o Job recriou os topicos;
- Order e Audit se recuperaram sem reinicio manual depois que a dependencia foi corrigida.

## Conceito Demonstrado

Troubleshooting orientado por sintomas, logs, eventos, estado de Jobs e verificacao direta da dependencia.
