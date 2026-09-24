Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

kubectl apply -k k8s
kubectl wait --for=condition=Available deployment/postgres -n microservices-lab --timeout=180s
kubectl wait --for=condition=Available deployment/kafka -n microservices-lab --timeout=240s
kubectl wait --for=condition=complete job/kafka-init -n microservices-lab --timeout=180s
kubectl wait --for=condition=Available deployment/inventory-service -n microservices-lab --timeout=240s
kubectl wait --for=condition=Available deployment/order-service -n microservices-lab --timeout=240s
kubectl wait --for=condition=Available deployment/payment-service -n microservices-lab --timeout=180s
kubectl wait --for=condition=Available deployment/audit-worker -n microservices-lab --timeout=180s
kubectl wait --for=condition=Available deployment/otel-collector -n microservices-lab --timeout=180s
kubectl wait --for=condition=Available deployment/jaeger -n microservices-lab --timeout=180s
kubectl wait --for=condition=Available deployment/prometheus -n microservices-lab --timeout=180s
kubectl wait --for=condition=Available deployment/grafana -n microservices-lab --timeout=180s
