$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$path = Join-Path $PSScriptRoot 'Test-Integration.ps1'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }

# Load only helpers: never execute the integration runner or stop containers.
$ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false) |
    ForEach-Object { Invoke-Expression $_.Extent.Text }

$BaseUrl = 'http://unused.invalid'
$TimeoutSeconds = 1
$script:response = $null
function Invoke-RestMethod { Write-Output -NoEnumerate $script:response }

$event1 = [pscustomobject]@{ eventType = 'OrderCreated'; eventId = 'one' }
$event2 = [pscustomobject]@{ eventType = 'PaymentApproved'; eventId = 'two' }
foreach ($count in @(0, 1, 2)) {
    $script:response = @(@($event1, $event2) | Select-Object -First $count)
    $actual = @(Request '/audit-events')
    Assert ($actual.Count -eq $count) "REST array enumeration failed for $count items"
    if ($count) { Assert ($actual[0].eventId -eq 'one') 'REST item was wrapped in an array' }
}

$script:response = $event1
Assert ((Request '/orders/one').eventId -eq 'one') 'REST object changed'
$script:response = @($event1, $event2)
Assert (@(AuditEvents 'test-order').Count -eq 2) 'Audit polling ignored existing payment event'

function Invoke-RestMethod { throw [Net.WebException]::new('test timeout', [Net.WebExceptionStatus]::Timeout) }
$caught = $false
try { Expect-HttpError '/stream' 'GET' $null @(502) | Out-Null } catch { $caught = $true }
Assert $caught 'Normal HTTP checks must not accept timeouts'
$result = Expect-HttpError '/stream' 'GET' $null @(502) -AllowTimeout
Assert ($result -like 'Request timed out*') 'Explicit outage timeout was not recorded'
Write-Host 'PASSOU: REST arrays (0/1/2), object response, audit polling, strict timeout, explicit outage timeout'
