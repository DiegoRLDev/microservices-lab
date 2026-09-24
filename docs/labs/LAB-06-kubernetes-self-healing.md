# LAB 06 - Kubernetes Self-Healing

## Objetivo

Apagar um Pod e observar o Deployment recriando outro automaticamente.

## Pre-Requisitos

Kubernetes aplicado:

```powershell
docker compose build inventory-service order-service payment-service audit-worker
kubectl apply -k k8s
kubectl get pods -n microservices-lab
```

Garantir 1 replica para o lab:

```powershell
kubectl scale deployment/inventory-service --replicas=1 -n microservices-lab
kubectl rollout status deployment/inventory-service -n microservices-lab
```

## Passos

Em um terminal, observar:

```powershell
kubectl get pods -n microservices-lab -l app=inventory-service -o wide -w
```

Em outro terminal, apagar um Pod:

```powershell
$pod = kubectl get pods -n microservices-lab -l app=inventory-service -o jsonpath='{.items[0].metadata.name}'
kubectl delete pod $pod -n microservices-lab
```

## O Que Observar

```text
Deployment deseja 1 Pod
        ↓
kubectl delete pod
        ↓
Actual = 0
        ↓
Kubernetes cria novo Pod
        ↓
Actual = Desired = 1
```

## Resultado Esperado

Um Pod antigo entra em `Terminating` e outro aparece ate ficar `Running` e `Ready`.

## Limpeza

Nao ha limpeza obrigatoria.

## Conceito Demonstrado

Pod e descartavel. Deployment mantem desired state.
