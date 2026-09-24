Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

docker compose build inventory-service order-service payment-service audit-worker
