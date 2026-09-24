# Guia gRPC

Este guia documenta o uso de gRPC no MicroservicesLab.

## Fluxo Atual

```text
Order.Service
     |
     | CheckAvailability
     | gRPC
     v
Inventory.Service
```

`Order.Service` usa gRPC para perguntar ao `Inventory.Service` se existe estoque suficiente antes de criar um pedido.

## Arquivos Reais

- Contrato: [src/protos/inventory.proto](../../src/protos/inventory.proto)
- Servidor gRPC: [src/services/Inventory.Service/Grpc/InventoryGrpcService.cs](../../src/services/Inventory.Service/Grpc/InventoryGrpcService.cs)
- Cliente gRPC: [src/services/Order.Service/Infrastructure/Grpc/GrpcInventoryClient.cs](../../src/services/Order.Service/Infrastructure/Grpc/GrpcInventoryClient.cs)
- Interface do cliente: [src/services/Order.Service/Infrastructure/Grpc/IInventoryClient.cs](../../src/services/Order.Service/Infrastructure/Grpc/IInventoryClient.cs)
- Configuracao do endpoint: [src/services/Order.Service/appsettings.json](../../src/services/Order.Service/appsettings.json)

## Papel do Proto

O arquivo `.proto` descreve a fronteira entre os servicos:

```text
service Inventory
  CheckAvailability
  ReserveStock
```

Ele define requests e responses de forma independente da implementacao. O `Inventory.Service` gera codigo de servidor a partir dele, e o `Order.Service` gera codigo de cliente.

No `Inventory.Service`:

```xml
<Protobuf Include="..\..\protos\inventory.proto" GrpcServices="Server" />
```

No `Order.Service`:

```xml
<Protobuf Include="..\..\protos\inventory.proto" GrpcServices="Client" />
```

## Request e Response

`Order.Service` envia:

```text
product_id
quantity
```

`Inventory.Service` responde:

```text
product_id
is_available
requested_quantity
available_quantity
unit_price
```

O `unit_price` trafega como string no proto para evitar problema de precisao decimal entre linguagens.

## HTTP/2 e HTTPS Local

gRPC em ASP.NET Core usa HTTP/2. No ambiente local atual, o `Inventory.Service` expoe gRPC em:

```text
https://localhost:7242
```

Esse endereco e configurado no `Order.Service`:

```json
{
  "InventoryGrpc": {
    "Address": "https://localhost:7242"
  }
}
```

## Executar os Servicos

Terminal 1:

```powershell
dotnet run --project .\src\services\Inventory.Service\Inventory.Service.csproj --launch-profile https
```

Terminal 2:

```powershell
dotnet run --project .\src\services\Order.Service\Order.Service.csproj --launch-profile http
```

## Testar a Integracao

Crie ou liste produtos:

```powershell
Invoke-RestMethod http://localhost:5276/products | ConvertTo-Json -Depth 10
```

Crie um pedido usando um `ProductId` existente:

```powershell
$body = @{ productId = "<PRODUCT_ID>"; quantity = 1 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5074/orders -ContentType "application/json" -Body $body
```

Se houver estoque, `Order.Service` recebe o preco via gRPC e persiste o pedido com `UnitPrice` e `TotalPrice`.

## Certificado Local

Se aparecer erro parecido com `UntrustedRoot`, o certificado HTTPS de desenvolvimento do .NET ainda nao esta confiavel na maquina.

Comando usado para corrigir:

```powershell
dotnet dev-certs https --trust
```

Depois disso, feche e reabra os servicos.

## gRPC vs Kafka

gRPC:

```text
Inventory, preciso que voce me responda agora.
```

Kafka:

```text
Um evento aconteceu; consumidores interessados podem reagir.
```

No projeto, gRPC participa da decisao imediata de criar o pedido. Kafka propaga fatos depois que eles acontecem, como `OrderCreated`, `PaymentApproved` e `PaymentRejected`.
