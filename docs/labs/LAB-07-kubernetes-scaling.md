# LAB 07 - Kubernetes Scaling

## Objetivo

Escalar `Inventory.Service` de 1 para 3 replicas e observar que o Service passa a ter multiplos backends.

## Pre-Requisitos

Kubernetes aplicado:

```powershell
kubectl apply -k k8s
```

## Passos

Comecar com 1 replica:

```powershell
kubectl scale deployment/inventory-service --replicas=1 -n microservices-lab
kubectl rollout status deployment/inventory-service -n microservices-lab
```

Escalar para 3:

```powershell
kubectl scale deployment/inventory-service --replicas=3 -n microservices-lab
kubectl rollout status deployment/inventory-service -n microservices-lab
kubectl get pods -n microservices-lab -l app=inventory-service -o wide
```

Observar EndpointSlices:

```powershell
kubectl get endpointslices -n microservices-lab -l kubernetes.io/service-name=inventory-service
kubectl describe endpointslice -n microservices-lab -l kubernetes.io/service-name=inventory-service
```

Voltar para 1:

```powershell
kubectl scale deployment/inventory-service --replicas=1 -n microservices-lab
kubectl rollout status deployment/inventory-service -n microservices-lab
```

## O Que Observar

- ha tres Pods do Inventory;
- o Service `inventory-service` continua com o mesmo nome;
- EndpointSlices apontam para os backends atuais.

## Resultado Esperado

```text
Inventory replicas: 3
Service: inventory-service
Backends: 3 endpoints nos EndpointSlices
```

## Limpeza

Voltar para 1 replica, como mostrado acima.

## Conceito Demonstrado

Service Discovery e load balancing interno por Service.
