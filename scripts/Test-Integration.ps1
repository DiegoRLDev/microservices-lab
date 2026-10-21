[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:4200',
    [switch]$IncludeFailures,
    [ValidateRange(15, 300)][int]$TimeoutSeconds = 90,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$BaseUrl = $BaseUrl.TrimEnd('/')
$runId = 'phase29-2-' + [guid]::NewGuid().ToString('N')
$results = [System.Collections.Generic.List[object]]::new()
$createdOrders = [System.Collections.Generic.List[string]]::new()
$restore = [System.Collections.Generic.HashSet[string]]::new()
$script:initialState = @()
$scenarioNames = @(
    'Orders and audit', 'Invalid input and insufficient stock', 'Price stream and Kafka snapshot',
    'Payment unavailable and recovery', 'Inventory unavailable and recovery',
    'Pricing unavailable, SSE independent', 'PriceFeed unavailable and recovery', 'Audit unavailable and catch-up'
)

function Compose {
    param([string[]]$Arguments)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command docker -ErrorAction Stop).Source
    $allArguments = @('compose', '--project-directory', $root) + $Arguments
    $start.Arguments = ($allArguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) {
            $process.Kill()
            throw "docker compose $Arguments timed out after 60s"
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "docker compose $Arguments failed: $errorOutput" }
        return $output
    } finally { $process.Dispose() }
}

function Container-State {
    $json = Compose -Arguments @('ps', '--all', '--format', 'json')
    @($json -split "`n" | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
}

function Request {
    param([string]$Path, [string]$Method = 'GET', $Body)
    $options = @{ Uri = "$BaseUrl$Path"; Method = $Method; TimeoutSec = 15; UseBasicParsing = $true }
    if ($null -ne $Body) {
        $options.ContentType = 'application/json'
        $options.Body = $Body | ConvertTo-Json -Compress
    }
    # Windows PowerShell 5.1 writes REST arrays as one pipeline item.
    $response = Invoke-RestMethod @options
    if ($response -is [array]) {
        foreach ($item in $response) { $item }
    } else { $response }
}

function Assert {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Wait-Until {
    param([scriptblock]$Probe, [string]$Description)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $value = & $Probe
        if ($value) { return $value }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timeout ($TimeoutSeconds s): $Description"
}

function Scenario {
    param([string]$Name, [string]$Expected, [scriptblock]$Test)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $before = @(Container-State)
    try {
        $evidence = & $Test
        $result = [ordered]@{ scenario = $Name; expected = $Expected; status = 'PASSOU'; seconds = [math]::Round($timer.Elapsed.TotalSeconds, 2); evidence = $evidence }
    } catch {
        $result = [ordered]@{ scenario = $Name; expected = $Expected; status = 'FALHOU'; seconds = [math]::Round($timer.Elapsed.TotalSeconds, 2); evidence = $_.Exception.Message }
    }
    $result.beforeContainers = $before
    $result.afterContainers = @(Container-State)
    $results.Add([pscustomobject]$result)
    Write-Host "$($result.status): $Name ($($result.seconds)s)"
    if ($restore.Count -gt 0) { throw 'Service restoration is incomplete; stopping subsequent scenarios.' }
}

function Stop-TestService {
    param([string]$Service)
    $restore.Add($Service) | Out-Null
    Compose -Arguments @('stop', '--timeout', '10', $Service) | Out-Null
    Assert (@(Container-State | Where-Object { $_.Service -eq $Service -and $_.State -eq 'exited' }).Count -eq 1) "Service did not stop: $Service"
}

function Restore-TestService {
    param([string]$Service)
    Compose -Arguments @('start', $Service) | Out-Null
    Wait-Until {
        $state = @(Container-State | Where-Object { $_.Service -eq $Service -and $_.State -eq 'running' })
        if ($state.Count -eq 1) { $state }
    } "$Service running after restart" | Out-Null
    $restore.Remove($Service) | Out-Null
}

function New-TestOrder {
    $order = Request '/api/order/orders' 'POST' @{ productId = $script:product.id; quantity = 1 }
    Assert ([bool]$order.id) 'Order ID missing'
    $createdOrders.Add([string]$order.id)
    return $order
}

function TerminalOrder {
    param([string]$Id)
    Wait-Until {
        $order = Request "/api/order/orders/$Id"
        if ([string]$order.status -in @('2', '3', 'Paid', 'PaymentRejected')) { return $order }
    } "terminal order $Id"
}

function AuditEvents {
    param([string]$Id)
    @(Wait-Until {
        $events = @(Request "/api/audit/audit-events?messageKey=$Id&limit=200")
        if ('OrderCreated' -in $events.eventType -and (@($events | Where-Object { $_.eventType -in @('PaymentApproved', 'PaymentRejected') }).Count -ge 1)) { return $events }
    } "audit events for $Id")
}

function Expect-HttpError {
    param([string]$Path, [string]$Method, $Body, [int[]]$Expected, [switch]$AllowTimeout)
    try { Request $Path $Method $Body | Out-Null } catch {
        if ($AllowTimeout -and $_.Exception -is [Net.WebException] -and $_.Exception.Status -eq [Net.WebExceptionStatus]::Timeout) {
            return 'Request timed out while upstream container was confirmed stopped'
        }
        if ($null -eq $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
        Assert ($status -in $Expected) "Unexpected HTTP $status; expected $Expected"
        return $status
    }
    throw "Expected HTTP error for $Method $Path"
}

function Read-Sse {
    Add-Type -AssemblyName System.Net.Http
    $client = [Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(12)
    $response = $null
    $reader = $null
    try {
        $task = $client.GetAsync("$BaseUrl/api/pricefeed/prices/stream", [Net.Http.HttpCompletionOption]::ResponseHeadersRead)
        Assert ($task.Wait(12000)) 'SSE headers timed out'
        $response = $task.GetAwaiter().GetResult()
        $response.EnsureSuccessStatusCode() | Out-Null
        Assert ($response.Content.Headers.ContentType.MediaType -eq 'text/event-stream') 'Not an SSE response'
        $reader = [IO.StreamReader]::new($response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
        $events = [System.Collections.Generic.List[object]]::new()
        $deadline = [DateTime]::UtcNow.AddSeconds(8)
        $eventType = ''
        while ($events.Count -lt 12 -and [DateTime]::UtcNow -lt $deadline) {
            $lineTask = $reader.ReadLineAsync()
            Assert ($lineTask.Wait(2000)) 'SSE data timed out'
            $line = $lineTask.GetAwaiter().GetResult()
            Assert ($null -ne $line) 'SSE ended before receiving enough events'
            if ($line.StartsWith('event:')) { $eventType = $line.Substring(6).Trim() }
            if ($line.StartsWith('data:')) {
                Assert ($eventType -eq 'price-update') 'Unexpected SSE event type'
                $event = $line.Substring(5).Trim() | ConvertFrom-Json
                Assert ([bool]$event.symbol -and $event.price -gt 0 -and [bool]$event.occurredAt) 'Invalid SSE payload'
                [DateTimeOffset]::Parse($event.occurredAt) | Out-Null
                $events.Add($event)
            }
        }
        Assert ($events.Count -ge 6) 'Too few SSE events'
        return $events.ToArray()
    } finally {
        if ($reader) { $reader.Dispose() }
        if ($response) { $response.Dispose() }
        $client.Dispose()
    }
}

try {
    $script:initialState = @(Container-State)
    foreach ($service in @('postgres', 'kafka', 'inventory-service', 'order-service', 'payment-service', 'audit-worker', 'pricefeed-service', 'pricing-service', 'frontend')) {
        Assert (@($script:initialState | Where-Object { $_.Service -eq $service -and $_.State -eq 'running' }).Count -eq 1) "Required service not running: $service"
    }
    $script:product = Request '/api/inventory/products' 'POST' @{ name = $runId; price = 12.34; availableQuantity = 10 }
    Assert ([bool]$script:product.id) 'Product creation failed'

    Scenario 'Orders and audit' 'Terminal payment, correlated unique audit events and unchanged stock' {
        $order = New-TestOrder
        $final = TerminalOrder $order.id
        $events = @(AuditEvents $order.id)
        Assert (@($events.eventId | Sort-Object -Unique).Count -eq $events.Count) 'Duplicate EventId'
        Assert (@($events | Where-Object { $_.eventType -eq 'OrderCreated' }).Count -eq 1) 'Expected one OrderCreated'
        $filtered = @(Request "/api/audit/audit-events?messageKey=$($order.id)&eventType=OrderCreated&limit=20")
        Assert ($filtered.Count -eq 1) 'Audit type/key filter mismatch'
        foreach ($event in $events) {
            $payload = $event.payload | ConvertFrom-Json
            Assert ($payload.eventId -eq $event.eventId -and $payload.data.orderId -eq $order.id -and $event.messageKey -eq $order.id) 'Audit envelope/key mismatch'
        }
        $orderId = ([guid]$order.id).ToString()
        $databaseRows = Compose -Arguments @('exec', '-T', 'postgres', 'psql', '-U', 'postgres', '-d', 'audit_db', '-At', '-c', "SELECT count(*) || ',' || count(DISTINCT event_id) FROM audit_events WHERE message_key = '$orderId';")
        Assert ($databaseRows.Trim() -eq "$($events.Count),$($events.Count)") 'Audit database/API counts differ'
        $persisted = Compose -Arguments @('exec', '-T', 'postgres', 'psql', '-U', 'postgres', '-d', 'order_db', '-At', '-c', "SELECT count(*) FROM orders WHERE id = '$orderId';")
        Assert ($persisted.Trim() -eq '1') 'Order missing in order_db'
        $createdEvent = $events | Where-Object { $_.eventType -eq 'OrderCreated' }
        $kafkaMessage = Compose -Arguments @('exec', '-T', 'kafka', '/opt/kafka/bin/kafka-console-consumer.sh', '--bootstrap-server', 'localhost:29092', '--topic', 'order-created', '--partition', [string]$createdEvent.partition, '--offset', [string]$createdEvent.offset, '--max-messages', '1', '--timeout-ms', '10000', '--property', 'print.key=true')
        Assert ($kafkaMessage.Contains($orderId) -and $kafkaMessage.Contains([string]$createdEvent.eventId)) 'Kafka message differs from audit envelope'
        Assert (@(Request '/api/order/orders' | Where-Object { $_.id -eq $order.id }).Count -eq 1) 'Order absent from frontend proxy list'
        $stock = Request "/api/inventory/products/$($script:product.id)"
        Assert ($stock.availableQuantity -eq 10) 'Unexpected stock mutation'
        @{ initial = $order; final = $final; events = $events; stock = $stock.availableQuantity; auditDatabaseCounts = $databaseRows.Trim(); kafkaMessage = $kafkaMessage.Trim() }
    }

    Scenario 'Invalid input and insufficient stock' 'HTTP 400 and no new order' {
        $before = @(Request '/api/order/orders' | Where-Object { $_.productId -eq $script:product.id }).Count
        $statuses = @(
            Expect-HttpError '/api/order/orders' 'POST' @{ productId = $script:product.id; quantity = 11 } @(400)
            Expect-HttpError '/api/order/orders' 'POST' @{ productId = $script:product.id; quantity = 0 } @(400)
            Expect-HttpError '/api/order/orders' 'POST' @{ productId = [guid]::Empty.ToString(); quantity = 1 } @(400)
            Expect-HttpError '/api/audit/audit-events?limit=999' 'GET' $null @(400)
        )
        Assert (@(Request '/api/order/orders' | Where-Object { $_.productId -eq $script:product.id }).Count -eq $before) 'Rejected input persisted an order'
        @{ statuses = $statuses; ordersBefore = $before }
    }

    Scenario 'Price stream and Kafka snapshot' 'Valid multi-symbol SSE and advancing REST timestamps' {
        $before = @(Request '/api/pricing/prices')
        $sse = @(Read-Sse)
        Assert (@($sse.symbol | Sort-Object -Unique).Count -gt 1) 'Expected multiple symbols'
        $changedSymbols = @($sse | Group-Object symbol | Where-Object { @($_.Group.price | Sort-Object -Unique).Count -gt 1 })
        Assert ($changedSymbols.Count -gt 0) 'No price movement observed in this SSE sample'
        $after = @(Wait-Until {
            $prices = @(Request '/api/pricing/prices')
            foreach ($price in $prices) {
                $old = $before | Where-Object { $_.symbol -eq $price.symbol }
                if ($old -and [DateTimeOffset]::Parse($price.occurredAt) -gt [DateTimeOffset]::Parse($old.occurredAt)) { return $prices }
            }
        } 'Kafka-derived snapshot advancing')
        Assert (@($after | Where-Object { $_.price -le 0 -or -not $_.symbol }).Count -eq 0) 'Invalid REST prices'
        @{ before = $before; after = $after; sse = $sse }
    }

    if ($IncludeFailures) {
        Scenario 'Payment unavailable and recovery' 'Pending while stopped, terminal after restart' {
            Stop-TestService 'payment-service'
            try {
                $order = New-TestOrder
                $pending = Request "/api/order/orders/$($order.id)"
                Assert ([string]$pending.status -in @('1', 'Pending')) 'Expected Pending during payment outage'
            } finally { Restore-TestService 'payment-service' }
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $final = TerminalOrder $order.id
            @{ orderId = $order.id; during = $pending; final = $final; recoverySeconds = $watch.Elapsed.TotalSeconds; events = @(AuditEvents $order.id) }
        }

        Scenario 'Inventory unavailable and recovery' 'HTTP 503, no persisted order, health recovers' {
            $before = @(Request '/api/order/orders' | Where-Object { $_.productId -eq $script:product.id }).Count
            Stop-TestService 'inventory-service'
            try {
                $status = Expect-HttpError '/api/order/orders' 'POST' @{ productId = $script:product.id; quantity = 1 } @(503)
                Assert (@(Request '/api/order/orders' | Where-Object { $_.productId -eq $script:product.id }).Count -eq $before) 'Order persisted during inventory outage'
            } finally { Restore-TestService 'inventory-service' }
            Wait-Until { try { Request '/api/inventory/health' } catch { $null } } 'Inventory health recovery' | Out-Null
            @{ status = $status; product = Request "/api/inventory/products/$($script:product.id)" }
        }

        Scenario 'Pricing unavailable, SSE independent' 'REST unavailable, real SSE events continue, REST recovers' {
            Stop-TestService 'pricing-service'
            try {
                $status = Expect-HttpError '/api/pricing/prices' 'GET' $null @(502, 503, 504) -AllowTimeout
                $events = @(Read-Sse)
            } finally { Restore-TestService 'pricing-service' }
            $prices = @(Wait-Until { try { $p = @(Request '/api/pricing/prices'); if ($p.Count -gt 0) { $p } } catch { $null } } 'Pricing snapshot recovery')
            @{ duringHttp = $status; sseEvents = $events.Count; recovered = $prices }
        }

        Scenario 'PriceFeed unavailable and recovery' 'SSE unavailable during outage, stream works after restart' {
            Stop-TestService 'pricefeed-service'
            # Nginx may wait longer for a stopped upstream than the HTTP probe timeout.
            try { $status = Expect-HttpError '/api/pricefeed/prices/stream' 'GET' $null @(502, 503, 504) -AllowTimeout }
            finally { Restore-TestService 'pricefeed-service' }
            Wait-Until { try { Request '/api/pricefeed/health' } catch { $null } } 'PriceFeed recovery' | Out-Null
            @{ duringHttp = $status; recoveredSse = @(Read-Sse) }
        }

        Scenario 'Audit unavailable and catch-up' 'Payment completes during audit outage, correlated events appear after restart' {
            Stop-TestService 'audit-worker'
            try { $order = New-TestOrder; $final = TerminalOrder $order.id }
            finally { Restore-TestService 'audit-worker' }
            Wait-Until { try { Request '/api/audit/health' } catch { $null } } 'Audit health recovery' | Out-Null
            @{ orderId = $order.id; final = $final; events = @(AuditEvents $order.id) }
        }
    }
} catch {
    $results.Add([pscustomobject]@{ scenario = 'Preconditions'; status = 'FALHOU'; evidence = $_.Exception.Message })
} finally {
    foreach ($service in @($restore)) {
        try { Compose -Arguments @('start', $service) | Out-Null } catch { $results.Add([pscustomobject]@{ scenario = "Restore $service"; status = 'FALHOU'; evidence = $_.Exception.Message }) }
    }
    foreach ($name in $scenarioNames) {
        if (-not @($results | Where-Object { $_.scenario -eq $name }).Count) {
            $results.Add([pscustomobject]@{ scenario = $name; status = 'NAO EXECUTADO'; evidence = 'Precondition failure, previous interruption, or IncludeFailures not selected.' })
        }
    }
    $results.Add([pscustomobject]@{ scenario = 'Kafka unavailable'; status = 'NAO EXECUTADO'; evidence = 'Known save-before-publish consistency gap; broker is never stopped by this script.' })
    try { $finalState = @(Container-State) } catch { $finalState = @(); $finalStateError = $_.Exception.Message }
    $report = [ordered]@{ runId = $runId; finishedAt = [DateTimeOffset]::Now.ToString('o'); product = $script:product; orderIds = $createdOrders.ToArray(); initialContainers = $script:initialState; finalContainers = $finalState; finalStateError = $finalStateError; scenarios = $results.ToArray() }
    if ($OutputPath) {
        $parent = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        $report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    }
    $results | Format-Table scenario, status, seconds -AutoSize | Out-Host
}
if (@($results | Where-Object { $_.status -eq 'FALHOU' }).Count -gt 0) { exit 1 }
