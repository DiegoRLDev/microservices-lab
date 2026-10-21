# Executando Localmente

Caminho recomendado: Docker Compose completo. Os comandos partem da raiz do repositorio e usam PowerShell. Para requisitos e portas publicadas veja tambem o [README](../../README.md).

## Docker Compose

```powershell
docker compose config --quiet
docker compose up -d --build
docker compose ps --all
docker compose logs --tail=60 order-service payment-service audit-worker
```

Angular: [localhost:4200](http://localhost:4200). Nao suba simultaneamente o Kafka isolado de `infra/kafka` nem outro PostgreSQL na porta 5432: sao modos alternativos, nao dependencias adicionais.

- Rede implicita: `microservices-lab_default`; servicos usam DNS interno.
- Kafka interno: `kafka:29092`; clientes no host: `localhost:9092`.
- Inventory gRPC interno: HTTP/2 sem TLS em `inventory-service:8081`, host 7242. Execucao local usa o perfil HTTPS, explicado abaixo.
- Topicos: `order-created`, `payment-approved`, `payment-rejected`, `price-updated`, com tres partitions, fator de replicacao 1.
- `kafka-init` cria topicos com `--if-not-exists`, encerra e deve exibir Exited (0). Erro intermediario agora interrompe o shell.
- Frontend espera Order, Inventory, Pricing e a inicializacao dos containers PriceFeed/Audit. Isso nao substitui readiness HTTP.
- As healthchecks Docker das APIs .NET testam TCP. HTTP `/health` testa banco em Order, Inventory e Audit; PriceFeed/Pricing nao atestam consumo Kafka.

## Verificacao somente leitura

```powershell
Invoke-RestMethod http://localhost:5074/health/ready
Invoke-RestMethod http://localhost:5276/health/ready
Invoke-RestMethod http://localhost:5288/health
Invoke-RestMethod http://localhost:5080/health
Invoke-RestMethod http://localhost:5090/health
Invoke-RestMethod http://localhost:4200/api/inventory/products
Invoke-RestMethod http://localhost:4200/api/order/orders
Invoke-RestMethod http://localhost:4200/api/pricing/prices
Invoke-RestMethod 'http://localhost:4200/api/audit/audit-events?limit=5'
curl.exe -N --max-time 3 http://localhost:4200/api/pricefeed/prices/stream
```

O curl do stream e limitado a 3s e normalmente termina com codigo 28 (timeout intencional de um stream continuo). Payment nao possui health HTTP: use status/logs, sem inferir prontidao apenas por estar running.

```powershell
docker compose exec -T kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:29092 --list
docker compose exec -T postgres psql -U postgres -d postgres -c "SELECT datname FROM pg_database WHERE datname IN ('order_db','inventory_db','audit_db') ORDER BY datname;"
```

## Persistencia e operacao

PostgreSQL usa `postgres_data` e Kafka usa `kafka_data`, prefixados pelo nome do projeto Compose. Bancos reais:

| Servico | Connection string (ambiente local didatico) |
| --- | --- |
| Order | `Host=postgres;Port=5432;Database=order_db;Username=postgres;Password=postgres` |
| Inventory | `Host=postgres;Port=5432;Database=inventory_db;Username=postgres;Password=postgres` |
| Audit | `Host=postgres;Port=5432;Database=audit_db;Username=postgres;Password=postgres` |

Nos appsettings locais, Host e localhost; nomes dos bancos permanecem iguais. `init-databases.sql` executa apenas em volume PostgreSQL novo. O Compose aplica migrations existentes automaticamente. Nao crie `orders_db` nem recrie bancos existentes para resolver divergencias.

Prometheus/Grafana/Jaeger nao possuem volumes nomeados de retencao configurados como PostgreSQL/Kafka; nao sao armazenamento de auditoria. Credenciais/portas sao de laboratorio, sem endurecimento para producao.

Comandos deliberados de operacao (interrompem servicos; nao sao parte do smoke):

```powershell
docker compose logs -f order-service
docker compose restart order-service
docker compose up -d --build frontend
docker compose down
```

`down` preserva os volumes nomeados. **Nao usar down -v, prune ou remover volumes como troubleshooting**: isso apaga bancos, mensagens e offsets. Nenhum desses comandos destrutivos e necessario para executar os testes.

## Desenvolvimento Angular

Com APIs ativas:

```powershell
Push-Location .\src\frontend\MicroservicesLab.App
pnpm install --frozen-lockfile
pnpm start --port 4201
Pop-Location
```

Use 4201 se o frontend Docker ja ocupar 4200; sem conflito, `pnpm start` usa 4200. O comando permanece ativo; Ctrl+C encerra.

`proxy.conf.json` remove os mesmos prefixos que Nginx:

| Prefixo | Backend no host | Backend no Docker |
| --- | --- | --- |
| /api/order | localhost:5074 | order-service:8080 |
| /api/inventory | localhost:5276 | inventory-service:8080 |
| /api/audit | localhost:5288 | audit-worker:8080 |
| /api/pricefeed | localhost:5080 | pricefeed-service:8080 |
| /api/pricing | localhost:5090 | pricing-service:8080 |

O Nginx desabilita buffering no proxy PriceFeed para entregar SSE. A SPA usa fallback para index.html. Configuracao de APIs e ferramentas fica em `src/environments/environment*.ts`, sem CORS ou gateway novo.

Estrutura basica em `src/frontend/MicroservicesLab.App`:
- `src/app/core/api`: HttpClient, contratos tipados, health e SSE.
- `src/app/pages`: dashboard, products, orders, prices, audit, observability.
- `src/app/app.routes.ts`: componentes standalone e rotas.
- `src/environments`, `proxy.conf.json`, `nginx.conf`: configuracao por modo de execucao.

## Execucao manual dos backends (alternativa)

Nao execute o servico local e seu equivalente Docker na mesma porta ou no mesmo consumer group para validar o fluxo. Confira o ambiente e as connection strings antes de trocar de modo. PostgreSQL/Kafka existentes podem ser reutilizados; instrucoes detalhadas: [PostgreSQL](POSTGRESQL.md), [Kafka](KAFKA.md).

Com dependencias ativas, executar cada comando abaixo em **terminal separado na raiz**. Migrations locais sao necessarias somente se ainda nao aplicadas; o Compose ja as aplica.

Inventory precisa do perfil HTTPS para a chamada gRPC local. Confie no certificado de desenvolvimento quando necessario:

```powershell
dotnet dev-certs https --trust
dotnet run --project .\src\services\Inventory.Service\Inventory.Service.csproj --launch-profile https
```

Order usa `https://localhost:7242` em appsettings local:

```powershell
dotnet run --project .\src\services\Order.Service\Order.Service.csproj --launch-profile http
```

Audit API em 5288 (sem depender de porta implicita):

```powershell
$env:ASPNETCORE_URLS = 'http://localhost:5288'
dotnet run --project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

Payment, sem servidor HTTP:

```powershell
Set-Location .\src\services\Payment.Service
go run ./cmd/payment-service
```

Git Bash usa barras normais: `cd ./src/services/Payment.Service`, `go run ./cmd/payment-service`. Nao use caminhos PowerShell com barras invertidas no Bash.

PriceFeed local ajustado para nao conflitar com Prometheus em 9090 e para combinar com o proxy Angular:

```powershell
Set-Location .\src\services\PriceFeed.Service
$env:PRICE_HTTP_ADDR = ':5080'
$env:PRICE_GRPC_ADDR = ':19090'
$env:PRICE_KAFKA_ENABLED = 'true'
$env:PRICE_KAFKA_BOOTSTRAP_SERVERS = 'localhost:9092'
$env:PRICE_UPDATE_INTERVAL = '500ms'
go run .
```

Use Ctrl+C para cancelamento dos contextos e encerramento. Para estudar somente SSE/console, `PRICE_KAFKA_ENABLED=false`; isso nao alimenta o snapshot Pricing. Simbolos/precos iniciais e labs: [Kafka PriceUpdated](../labs/LAB-15-kafka-price-updated.md).

Pricing REST local na porta esperada pelo proxy:

```powershell
$env:ASPNETCORE_URLS = 'http://localhost:5090'
dotnet run --project .\src\services\Pricing.Service\Pricing.Service.csproj --no-launch-profile
```

O modo gRPC direto e opcional e nao alimenta o snapshot REST: [mini-lab](../labs/LAB-RT-GRPC-SERVER-STREAMING.md).

## Testes e estudos

Comandos unitarios/build: [README](../../README.md#testes).
Integracao e falhas opcionais: [INTEGRATION_TESTS](../INTEGRATION_TESTS.md). Nao repetir as falhas controladas automaticamente em revisoes de documentacao.

Ferramentas: [Observabilidade](OBSERVABILITY.md) (Grafana 3000, Prometheus 9090, Jaeger 16686).
Kubernetes preserva o laboratorio da Parte I, nao uma implantacao completa da Parte III: [guia Kubernetes](KUBERNETES.md).
Outros procedimentos: [Referencia rapida](QUICK-REFERENCE.md), [Troubleshooting](TROUBLESHOOTING.md), [Indice dos labs](../labs/README.md).
