# Guia PostgreSQL

Este guia documenta o PostgreSQL usado pelo MicroservicesLab no estado atual do projeto.

## Bancos Atuais

```text
Order.Service     -> order_db
Inventory.Service -> inventory_db
Audit.Worker      -> audit_db
```

As connection strings reais estao em:

- [src/services/Order.Service/appsettings.json](../../src/services/Order.Service/appsettings.json)
- [src/services/Inventory.Service/appsettings.json](../../src/services/Inventory.Service/appsettings.json)
- [src/workers/Audit.Worker/appsettings.json](../../src/workers/Audit.Worker/appsettings.json)

## Database Ownership

Cada servico e dono do seu proprio banco.

`Order.Service` e dono de `order_db` e da tabela `orders`.

`Inventory.Service` e dono de `inventory_db` e da tabela `products`.

`Audit.Worker` e dono de `audit_db` e da tabela `audit_events`.

Um servico nao deve acessar diretamente tabelas pertencentes ao outro. Quando `Order.Service` precisa saber estoque, ele chama `Inventory.Service` via gRPC; ele nao consulta `inventory_db`. O `Audit.Worker` recebe eventos pelo Kafka; ele nao consulta os bancos dos servicos de negocio.

## Verificar Acesso

Verificar se existe algo respondendo em `localhost:5432`:

```powershell
Test-NetConnection localhost -Port 5432
```

Verificar container PostgreSQL usado nas validacoes locais:

```powershell
docker ps --filter "name=microserviceslab-postgres"
```

## Conectar

Usando `psql` dentro do container:

```powershell
docker exec -it microserviceslab-postgres psql -U postgres -d postgres
```

Executar um comando sem entrar no shell interativo:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d postgres -c "SELECT version();"
```

## Databases

Listar databases:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d postgres -c "\l"
```

Listar apenas os bancos do laboratorio:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d postgres -c "SELECT datname FROM pg_database WHERE datname IN ('order_db','inventory_db','audit_db') ORDER BY datname;"
```

Trocar database dentro do `psql` interativo:

```text
\c order_db
\c inventory_db
\c audit_db
```

## Tabelas

Listar tabelas do `order_db`:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d order_db -c "\dt"
```

Listar tabelas do `inventory_db`:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d inventory_db -c "\dt"
```

Listar tabelas do `audit_db`:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d audit_db -c "\dt"
```

Consultar pedidos:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d order_db -c "SELECT id, product_id, quantity, unit_price, total_price, status, created_at FROM orders ORDER BY created_at DESC LIMIT 10;"
```

Consultar produtos:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d inventory_db -c "SELECT id, name, price, available_quantity FROM products ORDER BY name;"
```

Consultar eventos auditados:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d audit_db -c "SELECT event_id, event_type, topic, message_key, kafka_partition, kafka_offset, occurred_at FROM audit_events ORDER BY audited_at DESC LIMIT 10;"
```

Consultar migrations do `Order.Service`:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d order_db -c "SELECT * FROM \"__EFMigrationsHistory\";"
```

Consultar migrations do `Inventory.Service`:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d inventory_db -c "SELECT * FROM \"__EFMigrationsHistory\";"
```

Consultar migrations do `Audit.Worker`:

```powershell
docker exec microserviceslab-postgres psql -U postgres -d audit_db -c "SELECT * FROM \"__EFMigrationsHistory\";"
```

## EF Core

Aplicar migrations do `Order.Service`:

```powershell
dotnet ef database update --project .\src\services\Order.Service\Order.Service.csproj --startup-project .\src\services\Order.Service\Order.Service.csproj
```

Aplicar migrations do `Inventory.Service`:

```powershell
dotnet ef database update --project .\src\services\Inventory.Service\Inventory.Service.csproj --startup-project .\src\services\Inventory.Service\Inventory.Service.csproj
```

Aplicar migrations do `Audit.Worker`:

```powershell
dotnet ef database update --project .\src\workers\Audit.Worker\Audit.Worker.csproj --startup-project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

Listar migrations do `Order.Service`:

```powershell
dotnet ef migrations list --project .\src\services\Order.Service\Order.Service.csproj --startup-project .\src\services\Order.Service\Order.Service.csproj
```

Listar migrations do `Inventory.Service`:

```powershell
dotnet ef migrations list --project .\src\services\Inventory.Service\Inventory.Service.csproj --startup-project .\src\services\Inventory.Service\Inventory.Service.csproj
```

Listar migrations do `Audit.Worker`:

```powershell
dotnet ef migrations list --project .\src\workers\Audit.Worker\Audit.Worker.csproj --startup-project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

Gerar script SQL do `Order.Service`:

```powershell
dotnet ef migrations script --project .\src\services\Order.Service\Order.Service.csproj --startup-project .\src\services\Order.Service\Order.Service.csproj
```

Gerar script SQL do `Inventory.Service`:

```powershell
dotnet ef migrations script --project .\src\services\Inventory.Service\Inventory.Service.csproj --startup-project .\src\services\Inventory.Service\Inventory.Service.csproj
```

Gerar script SQL do `Audit.Worker`:

```powershell
dotnet ef migrations script --project .\src\workers\Audit.Worker\Audit.Worker.csproj --startup-project .\src\workers\Audit.Worker\Audit.Worker.csproj
```

## Verificar Connection Strings

```powershell
Get-Content .\src\services\Order.Service\appsettings.json
Get-Content .\src\services\Inventory.Service\appsettings.json
Get-Content .\src\workers\Audit.Worker\appsettings.json
```

Valores atuais:

```text
OrdersDb: Host=localhost;Port=5432;Database=order_db;Username=postgres;Password=postgres
InventoryDb: Host=localhost;Port=5432;Database=inventory_db;Username=postgres;Password=postgres
AuditDb: Host=localhost;Port=5432;Database=audit_db;Username=postgres;Password=postgres
```
