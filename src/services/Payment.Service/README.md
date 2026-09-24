# Payment.Service

`Payment.Service` e o consumidor Kafka em Go introduzido na FASE 5.

Nesta fase ele consome `OrderCreated` do topico `order-created`, desserializa o envelope JSON produzido pelo `Order.Service`, valida o tipo do evento, confere se a Kafka message key corresponde ao `OrderId`, simula uma decisao de pagamento e publica `PaymentApproved` ou `PaymentRejected`.

## Escopo da FASE 5

Incluido:

- modulo Go em `src/services/Payment.Service`;
- consumidor Kafka com consumer group `payment-service`;
- contrato JSON proprio em Go para `OrderCreated`;
- simulacao local de aprovacao/rejeicao;
- publicacao de `PaymentApproved` no topico `payment-approved`;
- publicacao de `PaymentRejected` no topico `payment-rejected`;
- graceful shutdown com `Ctrl+C`;
- testes unitarios para configuracao, contrato e regra de pagamento.

Fora do escopo:

- banco `payment_db`;
- persistencia de `Payment`;
- atualizacao de status do pedido;
- Docker Compose completo.

Esses itens continuam planejados para as fases seguintes.

## Configuracao

Variaveis de ambiente:

```text
KAFKA_BROKERS=localhost:9092
KAFKA_ORDER_CREATED_TOPIC=order-created
KAFKA_PAYMENT_APPROVED_TOPIC=payment-approved
KAFKA_PAYMENT_REJECTED_TOPIC=payment-rejected
KAFKA_GROUP_ID=payment-service
PAYMENT_MAX_AUTO_APPROVAL_AMOUNT=10000
```

Todas possuem valores padrao.

## Executar Localmente

Suba o Kafka isolado da FASE 4.1:

```powershell
docker compose -f .\infra\kafka\docker-compose.kafka.yml up -d
.\infra\kafka\create-topics.ps1
```

Execute o consumidor:

```powershell
cd .\src\services\Payment.Service
go run .\cmd\payment-service
```

No Git Bash, a partir da raiz do repositorio:

```bash
cd ./src/services/Payment.Service
go run ./cmd/payment-service
```

No Git Bash, se ja estiver em `src/services/Payment.Service`:

```bash
go run ./cmd/payment-service
```

Em outro terminal, gere um pedido pelo fluxo ja validado:

```powershell
dotnet run --project .\src\services\Inventory.Service\Inventory.Service.csproj --launch-profile https
dotnet run --project .\src\services\Order.Service\Order.Service.csproj --launch-profile http
```

Quando `Order.Service` publicar `OrderCreated`, o `Payment.Service` deve registrar a simulacao do pagamento no log.

## Testes

```powershell
cd .\src\services\Payment.Service
go test ./...
```

No Git Bash, se ja estiver em `src/services/Payment.Service`:

```bash
go test ./...
```
