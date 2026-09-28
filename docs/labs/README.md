# Labs do MicroservicesLab

Os labs sao roteiros curtos para repetir os principais comportamentos do projeto sem reler todos os guias operacionais.

Antes de comecar, escolha um modo de execucao:

```text
Docker Compose: docker compose up -d --build
Kubernetes:     kubectl apply -k k8s
```

## Labs Disponiveis

| Lab | Tema |
| --- | --- |
| [LAB 01](LAB-01-happy-path.md) | Happy Path |
| [LAB 02](LAB-02-estoque-insuficiente.md) | Estoque insuficiente |
| [LAB 03](LAB-03-resiliencia-inventory-offline.md) | Resiliencia com Inventory offline |
| [LAB 04](LAB-04-kafka.md) | Kafka, topicos, keys e payloads |
| [LAB 05](LAB-05-observabilidade.md) | Grafana, Prometheus e Jaeger |
| [LAB 06](LAB-06-kubernetes-self-healing.md) | Kubernetes Self-Healing |
| [LAB 07](LAB-07-kubernetes-scaling.md) | Kubernetes Scaling |
| [LAB 08](LAB-08-troubleshooting-kubernetes-kafka.md) | Troubleshooting Kubernetes/Kafka |
| [Mini-lab Realtime gRPC](LAB-RT-GRPC-SERVER-STREAMING.md) | gRPC Server Streaming entre PriceFeed.Service e Pricing.Service |

## Guias de Apoio

- [Executando localmente](../guides/RUNNING-LOCALLY.md)
- [Quick Reference](../guides/QUICK-REFERENCE.md)
- [Troubleshooting](../guides/TROUBLESHOOTING.md)
- [Kubernetes](../guides/KUBERNETES.md)
