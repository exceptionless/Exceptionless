#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Live API surface audit for Exceptionless serialization testing.

.DESCRIPTION
    Posts events and other entities to a running Exceptionless API using all casing
    conventions (snake_case, camelCase, PascalCase, mixed) and captures the raw
    HTTP request, API response, and Elasticsearch document for each scenario.

    Output is saved to: audit-output/{AuditRunId}/{BranchName}/{scenario}/
      - request.json   : the body sent to the API
      - response.json  : the API response (GET after processing)
      - elastic.json   : the raw ES document

.EXAMPLE
    # Run against the running feature branch app (auto-detect branch)
    pwsh .agents/skills/serialization-audit/scripts/audit-api-surface.ps1

    # Run with explicit branch name and run ID
    pwsh .agents/skills/serialization-audit/scripts/audit-api-surface.ps1 -BranchName "main" -AuditRunId "live-2026-05-20"

    # Run against a different server
    pwsh .agents/skills/serialization-audit/scripts/audit-api-surface.ps1 -BaseUrl "http://localhost:5200" -EsUrl "http://localhost:9201"
#>
param(
    [string]$BaseUrl       = "http://localhost:7110",
    [string]$EsUrl         = "http://localhost:9200",
    [string]$BranchName    = "",         # Auto-detected from git if empty
    [string]$AuditRunId    = "live",
    [string]$UserEmail    = "admin@exceptionless.test",
    [string]$UserPassword = "tester",
    [string]$ProjectApiKey = "LhhP1C9gijpSKCslHHCvwdSIz298twx271nTest",
    [string]$UserApiKey    = "5f8aT5j0M1SdWCMOiJKCrlDNHMI38LjCH4LTTest",
    [int]$PollTimeoutSec   = 30,
    [int]$PollIntervalSec  = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Auto-detect branch name ──────────────────────────────────────────────────
if (-not $BranchName) {
    try {
        $BranchName = (git rev-parse --abbrev-ref HEAD 2>$null).Trim()
    } catch { }
    if (-not $BranchName) { $BranchName = "unknown" }
}
$BranchName = $BranchName -replace '[/\\]', '-'

# ─── Output directory ─────────────────────────────────────────────────────────
try {
    $RepoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel 2>$null).Trim()
} catch {
    $RepoRoot = ""
}

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
}

$OutputDir = Join-Path $RepoRoot "audit-output" $AuditRunId $BranchName
Write-Host "Output dir : $OutputDir"
Write-Host "Branch     : $BranchName"
Write-Host "Base URL   : $BaseUrl"
Write-Host "ES URL     : $EsUrl"

# ─── Helper: Save a scenario file ─────────────────────────────────────────────
function Save-ScenarioFile {
    param([string]$ScenarioName, [string]$FileName, [string]$Content)
    $dir = Join-Path $OutputDir $ScenarioName
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $Content | Set-Content -Path (Join-Path $dir $FileName) -Encoding utf8
}

# ─── Helper: Pretty-print JSON ────────────────────────────────────────────────
# Uses python3 json.tool to avoid PowerShell's ConvertFrom-Json date mangling
# (PowerShell auto-parses ISO 8601 strings and re-serializes in local timezone).
function Format-Json {
    param([string]$Json)
    try {
        $result = ($Json | python3 -c "import sys,json; print(json.dumps(json.loads(sys.stdin.read()), indent=2, ensure_ascii=False))" 2>$null) -join "`n"
        if ($LASTEXITCODE -eq 0 -and $result) { return $result }
    } catch { }
    # Fallback: return as-is if python3 unavailable
    return $Json
}

# ─── Helper: Invoke API request with error handling ───────────────────────────
function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$ApiKey,
        [string]$Body = $null,
        [hashtable]$Headers = @{}
    )
    $uri = "$BaseUrl/api/v2$Path"
    $allHeaders = @{ "Authorization" = "Bearer $ApiKey" } + $Headers

    $params = @{
        Method      = $Method
        Uri         = $uri
        Headers     = $allHeaders
        ErrorAction = "SilentlyContinue"
    }
    if ($Body) {
        $params["Body"]        = $Body
        $params["ContentType"] = "application/json"
    }

    try {
        $response = Invoke-WebRequest @params -UseBasicParsing
        return @{
            StatusCode = $response.StatusCode
            Body       = $response.Content
            Headers    = $response.Headers
        }
    } catch {
        # PowerShell 7: HttpResponseException; PS 5: WebException
        $statusCode = 0
        $body       = ""
        try {
            if ($_.Exception.Response) {
                $statusCode = [int]$_.Exception.Response.StatusCode
            }
        } catch { }
        try {
            # PS 7 HttpResponseException has a Body property
            if ($_.Exception.PSObject.Properties['Body']) {
                $body = $_.Exception.Body
            }
        } catch { }
        if (-not $body) { $body = $_.ErrorDetails.Message }
        if (-not $body) { $body = $_.Exception.Message }
        return @{
            StatusCode = $statusCode
            Body       = $body
            Headers    = @{}
            Error      = $_.Exception.Message
        }
    }
}

# ─── Helper: Poll until event appears by reference ID ─────────────────────────
function Wait-ForEventByRef {
    param([string]$ReferenceId)
    $elapsed = 0
    while ($elapsed -lt $PollTimeoutSec) {
        Start-Sleep -Seconds $PollIntervalSec
        $elapsed += $PollIntervalSec
        $resp = Invoke-Api -Method GET -Path "/events/by-ref/$ReferenceId" -ApiKey $UserApiKey
        if ($resp.StatusCode -eq 200 -and $resp.Body -and $resp.Body.Trim() -ne "[]") {
            return $resp.Body
        }
    }
    return $null
}

# ─── Helper: Query Elasticsearch for a document by ID ────────────────────────
function Get-EsDocument {
    param([string]$IndexPattern, [string]$DocId)
    # Step 1: find the concrete index
    $searchBody = "{`"query`":{`"ids`":{`"values`":[`"$DocId`"]}},`"size`":1,`"_source`":false}"
    $searchUrl  = "$EsUrl/$IndexPattern/_search?ignore_unavailable=true&allow_no_indices=true"
    try {
        $resolveResp = Invoke-WebRequest -Method POST -Uri $searchUrl `
            -Body $searchBody -ContentType "application/json" -UseBasicParsing -ErrorAction SilentlyContinue
        if ($resolveResp.StatusCode -eq 200) {
            $hits = ($resolveResp.Content | ConvertFrom-Json -Depth 10).hits.hits
            if ($hits -and $hits.Count -gt 0) {
                $concreteIndex = $hits[0]._index
                # Step 2: GET from concrete index
                $getUrl = "$EsUrl/$concreteIndex/_doc/$DocId"
                $getResp = Invoke-WebRequest -Method GET -Uri $getUrl `
                    -UseBasicParsing -ErrorAction SilentlyContinue
                if ($getResp.StatusCode -eq 200) {
                    $source = ($getResp.Content | ConvertFrom-Json -Depth 20)._source
                    return Format-Json ($source | ConvertTo-Json -Depth 20 -Compress:$false)
                }
            }
        }
    } catch { }
    return "{`"error`": `"Document $DocId not found in $IndexPattern`"}"
}

# ─── Helper: Run a full event POST scenario ───────────────────────────────────
function Invoke-EventScenario {
    param([string]$ScenarioName, [string]$RequestJson, [string]$ReferenceId)
    Write-Host "  → $ScenarioName" -NoNewline

    # 1. Save request
    Save-ScenarioFile -ScenarioName $ScenarioName -FileName "request.json" -Content (Format-Json $RequestJson)

    # 2. POST event
    $postResp = Invoke-Api -Method POST -Path "/events" -ApiKey $ProjectApiKey -Body $RequestJson
    if ($postResp.StatusCode -notin @(200, 201, 202)) {
        Write-Host " ✗ POST failed ($($postResp.StatusCode))"
        Save-ScenarioFile -ScenarioName $ScenarioName -FileName "response.json" `
            -Content "{`"error`": `"POST failed with status $($postResp.StatusCode)`", `"body`": $($postResp.Body)}"
        return
    }

    # 3. Poll for processed event
    Write-Host " ." -NoNewline
    $eventsJson = Wait-ForEventByRef -ReferenceId $ReferenceId
    if (-not $eventsJson) {
        Write-Host " ✗ timeout waiting for $ReferenceId"
        Save-ScenarioFile -ScenarioName $ScenarioName -FileName "response.json" `
            -Content "{`"error`": `"Timeout waiting for event with reference_id=$ReferenceId`"}"
        return
    }

    # 4. Parse out event id
    $events = $eventsJson | ConvertFrom-Json -Depth 10
    $event  = if ($events -is [array]) { $events[0] } else { $events }
    $eventId = $event.id

    # 5. GET full event by id for response
    $getResp = Invoke-Api -Method GET -Path "/events/$eventId" -ApiKey $UserApiKey
    if ($getResp.StatusCode -eq 200) {
        Save-ScenarioFile -ScenarioName $ScenarioName -FileName "response.json" `
            -Content (Format-Json $getResp.Body)
    } else {
        Save-ScenarioFile -ScenarioName $ScenarioName -FileName "response.json" `
            -Content (Format-Json $eventsJson)
    }

    # 6. Fetch ES document
    $esDoc = Get-EsDocument -IndexPattern "*-events-*" -DocId $eventId
    Save-ScenarioFile -ScenarioName $ScenarioName -FileName "elastic.json" -Content $esDoc

    Write-Host " ✓ (id=$eventId)"
}

# HEALTH CHECK
Write-Host "`n[Health Check] $BaseUrl/api/v2/about"
$health = Invoke-Api -Method GET -Path "/about" -ApiKey $UserApiKey
if ($health.StatusCode -ne 200) {
    Write-Host "✗ API not reachable (status $($health.StatusCode)). Start the app with 'aspire run' first." -ForegroundColor Red
    exit 1
}
Write-Host "✓ API is up"

# ─── Login to get a JWT for admin operations (token creation, webhooks) ───────
Write-Host "[Login] $UserEmail"
$loginBody = "{`"email`":`"$UserEmail`",`"password`":`"$UserPassword`"}"
$loginResp = Invoke-WebRequest -Method POST -Uri "$BaseUrl/api/v2/auth/login" `
    -Body $loginBody -ContentType "application/json" -UseBasicParsing -ErrorAction SilentlyContinue
$JwtToken = ""
if ($loginResp -and $loginResp.StatusCode -eq 200) {
    $JwtToken = ($loginResp.Content | ConvertFrom-Json -Depth 5).token
    Write-Host "✓ JWT obtained`n"
} else {
    Write-Host "⚠ Login failed, some scenarios may fail (token/webhook creation)`n"
}

# EVENT POST SCENARIOS (1-8)
Write-Host "[Events — POST]"

Invoke-EventScenario -ScenarioName "events-post-snake-case" -ReferenceId "audit-snake-001" -RequestJson @'
{
    "type": "error",
    "message": "Test error with snake_case payload",
    "date": "2026-05-20T12:00:00+00:00",
    "tags": ["audit", "snake_case"],
    "reference_id": "audit-snake-001",
    "count": 1,
    "value": 42.5,
    "geo": "40.7128,-74.0060",
    "data": {
        "custom_field": "custom_value",
        "nested_object": { "inner_key": "inner_value", "inner_number": 123 }
    },
    "@user": {
        "identity": "user@example.com",
        "name": "Test User",
        "data": { "plan_name": "premium" }
    },
    "@environment": {
        "o_s_name": "Windows 11",
        "o_s_version": "10.0.22621",
        "ip_address": "192.168.1.100",
        "machine_name": "AUDIT-MACHINE",
        "runtime_version": ".NET 8.0.1",
        "processor_count": 8,
        "total_physical_memory": 17179869184,
        "process_name": "AuditApp"
    },
    "@request": {
        "client_ip_address": "10.0.0.100",
        "http_method": "POST",
        "user_agent": "AuditAgent/1.0",
        "is_secure": true,
        "host": "audit.localhost",
        "path": "/api/audit?key=value&other=123",
        "port": 443,
        "cookies": { "session_id": "abc123" }
    },
    "@simple_error": {
        "message": "Null reference exception occurred",
        "type": "System.NullReferenceException",
        "stack_trace": "   at Audit.Tests.Run() in AuditTests.cs:line 42"
    }
}
'@

Invoke-EventScenario -ScenarioName "events-post-camel-case" -ReferenceId "audit-camel-001" -RequestJson @'
{
    "type": "error",
    "message": "Test error with camelCase payload",
    "date": "2026-05-20T12:00:00+00:00",
    "tags": ["audit", "camelCase"],
    "referenceId": "audit-camel-001",
    "count": 1,
    "value": 42.5,
    "geo": "40.7128,-74.0060",
    "data": {
        "customField": "custom_value",
        "nestedObject": { "innerKey": "inner_value", "innerNumber": 123 }
    },
    "@user": {
        "identity": "user@example.com",
        "name": "Test User",
        "data": { "planName": "premium" }
    },
    "@environment": {
        "osName": "Windows 11",
        "osVersion": "10.0.22621",
        "ipAddress": "192.168.1.100",
        "machineName": "AUDIT-MACHINE",
        "runtimeVersion": ".NET 8.0.1",
        "processorCount": 8,
        "processName": "AuditApp"
    },
    "@request": {
        "clientIpAddress": "10.0.0.100",
        "httpMethod": "POST",
        "userAgent": "AuditAgent/1.0",
        "isSecure": true,
        "host": "audit.localhost",
        "path": "/api/audit",
        "port": 443
    },
    "@simple_error": {
        "message": "Null reference exception occurred",
        "type": "System.NullReferenceException",
        "stackTrace": "   at Audit.Tests.Run() in AuditTests.cs:line 42"
    }
}
'@

Invoke-EventScenario -ScenarioName "events-post-pascal-case" -ReferenceId "audit-pascal-001" -RequestJson @'
{
    "Type": "error",
    "Message": "Test error with PascalCase payload",
    "Date": "2026-05-20T12:00:00+00:00",
    "Tags": ["audit", "PascalCase"],
    "ReferenceId": "audit-pascal-001",
    "Count": 1,
    "Value": 42.5,
    "Geo": "40.7128,-74.0060",
    "Data": {
        "CustomField": "custom_value",
        "NestedObject": { "InnerKey": "inner_value", "InnerNumber": 123 }
    },
    "@user": {
        "Identity": "user@example.com",
        "Name": "Test User",
        "Data": { "PlanName": "premium" }
    },
    "@environment": {
        "OSName": "Windows 11",
        "OSVersion": "10.0.22621",
        "IpAddress": "192.168.1.100",
        "MachineName": "AUDIT-MACHINE",
        "RuntimeVersion": ".NET 8.0.1",
        "ProcessorCount": 8,
        "ProcessName": "AuditApp"
    },
    "@request": {
        "ClientIpAddress": "10.0.0.100",
        "HttpMethod": "POST",
        "UserAgent": "AuditAgent/1.0",
        "IsSecure": true,
        "Host": "audit.localhost",
        "Path": "/api/audit",
        "Port": 443
    }
}
'@

Invoke-EventScenario -ScenarioName "events-post-mixed-case" -ReferenceId "audit-mixed-001" -RequestJson @'
{
    "type": "error",
    "Message": "Test error with mixed casing payload",
    "date": "2026-05-20T12:00:00+00:00",
    "Tags": ["audit", "mixed"],
    "referenceId": "audit-mixed-001",
    "Count": 1,
    "value": 42.5,
    "data": {
        "snake_field": "snake_value",
        "CamelField": "camel_value",
        "PascalField": "pascal_value"
    },
    "@user": {
        "identity": "user@example.com",
        "Name": "Test User"
    },
    "@request": {
        "ClientIpAddress": "10.0.0.100",
        "http_method": "POST",
        "UserAgent": "AuditAgent/1.0"
    }
}
'@

Invoke-EventScenario -ScenarioName "events-post-special-chars" -ReferenceId "audit-special-001" -RequestJson @'
{
    "type": "error",
    "message": "Test with special chars: <script>alert(\"xss\")</script> & \"quoted\" & 'single'",
    "date": "2026-05-20T12:00:00+00:00",
    "reference_id": "audit-special-001",
    "data": {
        "html_content": "<div class=\"test\">Hello & World</div>",
        "url_with_params": "https://example.com/path?foo=bar&baz=qux",
        "unicode_text": "Hello \u4e16\u754c \u0441\u0432\u0435\u0442 \u0639\u0627\u0644\u0645",
        "emoji_text": "Test \ud83d\ude80 emoji",
        "newline_text": "Line1\nLine2\tTabbed",
        "null_char": "before\u0000after",
        "backslash": "C:\\Users\\test\\file.txt"
    }
}
'@

Invoke-EventScenario -ScenarioName "events-post-numeric-edge-cases" -ReferenceId "audit-numeric-001" -RequestJson @'
{
    "type": "error",
    "message": "Test numeric edge cases",
    "date": "2026-05-20T12:00:00+00:00",
    "reference_id": "audit-numeric-001",
    "data": {
        "zero_int": 0,
        "zero_float": 0.0,
        "negative_zero": -0.0,
        "large_int": 9007199254740991,
        "negative_int": -2147483648,
        "double_val": 1.7976931348623157e+308,
        "small_double": 5e-324,
        "negative_double": -1.23456789e-100,
        "int_max": 2147483647,
        "one_point_zero": 1.0
    }
}
'@

Invoke-EventScenario -ScenarioName "events-post-null-empty" -ReferenceId "audit-null-001" -RequestJson @'
{
    "type": "error",
    "message": "Test null and empty values",
    "date": "2026-05-20T12:00:00+00:00",
    "reference_id": "audit-null-001",
    "tags": [],
    "data": {
        "null_value": null,
        "empty_string": "",
        "empty_object": {},
        "empty_array": []
    },
    "@user": null
}
'@

Invoke-EventScenario -ScenarioName "events-post-date-formats" -ReferenceId "audit-dates-001" -RequestJson @'
{
    "type": "error",
    "message": "Test various date formats",
    "date": "2026-05-20T12:00:00+00:00",
    "reference_id": "audit-dates-001",
    "data": {
        "iso_full": "2026-01-15T10:30:00.000Z",
        "iso_with_offset": "2026-01-15T10:30:00+05:30",
        "iso_no_ms": "2026-01-15T10:30:00Z",
        "date_only": "2026-01-15",
        "time_only": "10:30:00",
        "unix_timestamp": 1705312200,
        "unix_timestamp_ms": 1705312200000
    }
}
'@

# EVENT GET SCENARIOS (9-10)
Write-Host "`n[Events — GET]"

# events-get-list: submit an event, then GET the events list
$listRefId = "audit-list-001"
Write-Host "  → events-get-list" -NoNewline

$listPostResp = Invoke-Api -Method POST -Path "/events" -ApiKey $ProjectApiKey -Body @'
{
    "type": "log",
    "message": "Test list retrieval",
    "date": "2026-05-20T12:00:00+00:00",
    "reference_id": "audit-list-001",
    "source": "AuditSource-List"
}
'@

if ($listPostResp.StatusCode -in @(200, 201, 202)) {
    $listEventsJson = Wait-ForEventByRef -ReferenceId $listRefId
    if ($listEventsJson) {
        # GET the events list
        $listResp = Invoke-Api -Method GET -Path "/events?limit=10&sort=-date" -ApiKey $UserApiKey
        $requestObj = @{ method = "GET"; path = "/api/v2/events"; params = @{ limit = 10; sort = "-date" } }
        Save-ScenarioFile -ScenarioName "events-get-list" -FileName "request.json" `
            -Content (Format-Json ($requestObj | ConvertTo-Json -Depth 5))
        Save-ScenarioFile -ScenarioName "events-get-list" -FileName "response.json" `
            -Content (Format-Json $listResp.Body)

        # Also get ES doc for the first event in the list
        $events = $listEventsJson | ConvertFrom-Json -Depth 10
        $event  = if ($events -is [array]) { $events[0] } else { $events }
        $esDoc  = Get-EsDocument -IndexPattern "*-events-*" -DocId $event.id
        Save-ScenarioFile -ScenarioName "events-get-list" -FileName "elastic.json" -Content $esDoc
        Write-Host " ✓"
    } else { Write-Host " ✗ timeout" }
} else { Write-Host " ✗ POST failed ($($listPostResp.StatusCode))" }

# events-get-stack-mode
$stackModeRefId = "audit-stackmode-001"
Write-Host "  → events-get-stack-mode" -NoNewline

$stackModePostResp = Invoke-Api -Method POST -Path "/events" -ApiKey $ProjectApiKey -Body @'
{
    "type": "log",
    "message": "Test stack mode retrieval",
    "date": "2026-05-20T12:00:00+00:00",
    "reference_id": "audit-stackmode-001",
    "source": "AuditSource-StackMode"
}
'@

if ($stackModePostResp.StatusCode -in @(200, 201, 202)) {
    $smEventsJson = Wait-ForEventByRef -ReferenceId $stackModeRefId
    if ($smEventsJson) {
        $smResp = Invoke-Api -Method GET -Path "/events?mode=stack_new&limit=10" -ApiKey $UserApiKey
        $smRequestObj = @{ method = "GET"; path = "/api/v2/events"; params = @{ mode = "stack_new"; limit = 10 } }
        Save-ScenarioFile -ScenarioName "events-get-stack-mode" -FileName "request.json" `
            -Content (Format-Json ($smRequestObj | ConvertTo-Json -Depth 5))
        Save-ScenarioFile -ScenarioName "events-get-stack-mode" -FileName "response.json" `
            -Content (Format-Json $smResp.Body)

        $smEvents = $smEventsJson | ConvertFrom-Json -Depth 10
        $smEvent  = if ($smEvents -is [array]) { $smEvents[0] } else { $smEvents }
        $smStackId = $smEvent.stack_id
        if ($smStackId) {
            $smStackDoc = Get-EsDocument -IndexPattern "*-stacks-*" -DocId $smStackId
            Save-ScenarioFile -ScenarioName "events-get-stack-mode" -FileName "elastic.json" -Content $smStackDoc
        }
        Write-Host " ✓"
    } else { Write-Host " ✗ timeout" }
} else { Write-Host " ✗ POST failed ($($stackModePostResp.StatusCode))" }

# ORGANIZATION SCENARIOS (11-13)
Write-Host "`n[Organizations]"
$orgId = "537650f3b77efe23a47914f3"

# organizations-get-by-id
Write-Host "  → organizations-get-by-id" -NoNewline
$orgGetReq = @{ method = "GET"; path = "/api/v2/organizations/$orgId" }
Save-ScenarioFile -ScenarioName "organizations-get-by-id" -FileName "request.json" `
    -Content (Format-Json ($orgGetReq | ConvertTo-Json -Depth 5))
$orgGetResp = Invoke-Api -Method GET -Path "/organizations/$orgId" -ApiKey $UserApiKey
Save-ScenarioFile -ScenarioName "organizations-get-by-id" -FileName "response.json" `
    -Content (Format-Json $orgGetResp.Body)
$orgEsDoc = Get-EsDocument -IndexPattern "*-organizations-*" -DocId $orgId
Save-ScenarioFile -ScenarioName "organizations-get-by-id" -FileName "elastic.json" -Content $orgEsDoc
Write-Host " ✓"

# organizations-patch-snake-case
Write-Host "  → organizations-patch-snake-case" -NoNewline
$orgPatchSnakeBody = @'
{ "name": "Acme Corp (snake patched)" }
'@
Save-ScenarioFile -ScenarioName "organizations-patch-snake-case" -FileName "request.json" `
    -Content (Format-Json $orgPatchSnakeBody)
$orgPatchSnakeResp = Invoke-Api -Method PATCH -Path "/organizations/$orgId" `
    -ApiKey $UserApiKey -Body $orgPatchSnakeBody
Save-ScenarioFile -ScenarioName "organizations-patch-snake-case" -FileName "response.json" `
    -Content (Format-Json $orgPatchSnakeResp.Body)
$orgSnakeEsDoc = Get-EsDocument -IndexPattern "*-organizations-*" -DocId $orgId
Save-ScenarioFile -ScenarioName "organizations-patch-snake-case" -FileName "elastic.json" -Content $orgSnakeEsDoc
Write-Host " ✓"

# organizations-patch-camel-case
Write-Host "  → organizations-patch-camel-case" -NoNewline
$orgPatchCamelBody = @'
{ "name": "Acme Corp (camel patched)" }
'@
Save-ScenarioFile -ScenarioName "organizations-patch-camel-case" -FileName "request.json" `
    -Content (Format-Json $orgPatchCamelBody)
$orgPatchCamelResp = Invoke-Api -Method PATCH -Path "/organizations/$orgId" `
    -ApiKey $UserApiKey -Body $orgPatchCamelBody
Save-ScenarioFile -ScenarioName "organizations-patch-camel-case" -FileName "response.json" `
    -Content (Format-Json $orgPatchCamelResp.Body)
$orgCamelEsDoc = Get-EsDocument -IndexPattern "*-organizations-*" -DocId $orgId
Save-ScenarioFile -ScenarioName "organizations-patch-camel-case" -FileName "elastic.json" -Content $orgCamelEsDoc
Write-Host " ✓"

# PROJECT SCENARIOS (14-15)
Write-Host "`n[Projects]"
$projectId = "537650f3b77efe23a47914f4"

# projects-get-by-id
Write-Host "  → projects-get-by-id" -NoNewline
$projGetReq = @{ method = "GET"; path = "/api/v2/projects/$projectId" }
Save-ScenarioFile -ScenarioName "projects-get-by-id" -FileName "request.json" `
    -Content (Format-Json ($projGetReq | ConvertTo-Json -Depth 5))
$projGetResp = Invoke-Api -Method GET -Path "/projects/$projectId" -ApiKey $UserApiKey
Save-ScenarioFile -ScenarioName "projects-get-by-id" -FileName "response.json" `
    -Content (Format-Json $projGetResp.Body)
$projEsDoc = Get-EsDocument -IndexPattern "*-projects-*" -DocId $projectId
Save-ScenarioFile -ScenarioName "projects-get-by-id" -FileName "elastic.json" -Content $projEsDoc
Write-Host " ✓"

# projects-patch-snake-case
Write-Host "  → projects-patch-snake-case" -NoNewline
$projPatchBody = @'
{ "name": "My Project (snake patched)" }
'@
Save-ScenarioFile -ScenarioName "projects-patch-snake-case" -FileName "request.json" `
    -Content (Format-Json $projPatchBody)
$projPatchResp = Invoke-Api -Method PATCH -Path "/projects/$projectId" `
    -ApiKey $UserApiKey -Body $projPatchBody
Save-ScenarioFile -ScenarioName "projects-patch-snake-case" -FileName "response.json" `
    -Content (Format-Json $projPatchResp.Body)
$projPatchEsDoc = Get-EsDocument -IndexPattern "*-projects-*" -DocId $projectId
Save-ScenarioFile -ScenarioName "projects-patch-snake-case" -FileName "elastic.json" -Content $projPatchEsDoc
Write-Host " ✓"

# STACK SCENARIO (16)
Write-Host "`n[Stacks]"

# stacks-get-after-event: reuse a previously submitted event's stack
$stackRefId = "audit-stack-001"
Write-Host "  → stacks-get-after-event" -NoNewline

$stackPostResp = Invoke-Api -Method POST -Path "/events" -ApiKey $ProjectApiKey -Body @'
{
    "type": "error",
    "message": "Test stack capture",
    "date": "2026-05-20T12:00:00+00:00",
    "reference_id": "audit-stack-001",
    "source": "AuditSource-Stack",
    "@simple_error": {
        "message": "Stack test exception",
        "type": "System.InvalidOperationException",
        "stack_trace": "   at StackTest.Run() line 1"
    }
}
'@
if ($stackPostResp.StatusCode -in @(200, 201, 202)) {
    $stackEventsJson = Wait-ForEventByRef -ReferenceId $stackRefId
    if ($stackEventsJson) {
        $stackEvents = $stackEventsJson | ConvertFrom-Json -Depth 10
        $stackEvent  = if ($stackEvents -is [array]) { $stackEvents[0] } else { $stackEvents }
        $stackId     = $stackEvent.stack_id

        $stackGetReq = @{ method = "GET"; path = "/api/v2/stacks/$stackId" }
        Save-ScenarioFile -ScenarioName "stacks-get-after-event" -FileName "request.json" `
            -Content (Format-Json ($stackGetReq | ConvertTo-Json -Depth 5))

        $stackGetResp = Invoke-Api -Method GET -Path "/stacks/$stackId" -ApiKey $UserApiKey
        Save-ScenarioFile -ScenarioName "stacks-get-after-event" -FileName "response.json" `
            -Content (Format-Json $stackGetResp.Body)

        $stackEsDoc = Get-EsDocument -IndexPattern "*-stacks-*" -DocId $stackId
        Save-ScenarioFile -ScenarioName "stacks-get-after-event" -FileName "elastic.json" -Content $stackEsDoc
        Write-Host " ✓"
    } else { Write-Host " ✗ timeout" }
} else { Write-Host " ✗ POST failed ($($stackPostResp.StatusCode))" }

# TOKEN SCENARIO (17)
Write-Host "`n[Tokens]"
Write-Host "  → tokens-create-and-get" -NoNewline

$tokenBody = @'
{ "organization_id": "537650f3b77efe23a47914f3", "project_id": "537650f3b77efe23a47914f4", "scopes": ["client"] }
'@
Save-ScenarioFile -ScenarioName "tokens-create-and-get" -FileName "request.json" `
    -Content (Format-Json $tokenBody)
$tokenAuthKey = if ($JwtToken) { $JwtToken } else { $UserApiKey }
$tokenCreateResp = Invoke-Api -Method POST -Path "/tokens" -ApiKey $tokenAuthKey -Body $tokenBody
if ($tokenCreateResp.StatusCode -in @(200, 201)) {
    Save-ScenarioFile -ScenarioName "tokens-create-and-get" -FileName "response.json" `
        -Content (Format-Json $tokenCreateResp.Body)
    $tokenId = ($tokenCreateResp.Body | ConvertFrom-Json -Depth 5).id
    if ($tokenId) {
        $tokenEsDoc = Get-EsDocument -IndexPattern "*-tokens-*" -DocId $tokenId
        Save-ScenarioFile -ScenarioName "tokens-create-and-get" -FileName "elastic.json" -Content $tokenEsDoc
    }
    Write-Host " ✓"
} else {
    Save-ScenarioFile -ScenarioName "tokens-create-and-get" -FileName "response.json" `
        -Content "{`"error`": `"Create failed: $($tokenCreateResp.StatusCode)`", `"body`": $($tokenCreateResp.Body)}"
    Write-Host " ✗ ($($tokenCreateResp.StatusCode))"
}

# WEBHOOK SCENARIOS (18-19)
Write-Host "`n[Webhooks]"

$webhookAuthKey = if ($JwtToken) { $JwtToken } else { $UserApiKey }

# webhooks-create-snake-case
Write-Host "  → webhooks-create-snake-case" -NoNewline
$webhookSnakeBody = @'
{
    "organization_id": "537650f3b77efe23a47914f3",
    "project_id": "537650f3b77efe23a47914f4",
    "url": "https://audit.localhost/webhook/snake",
    "event_types": ["NewError", "NewEvent"]
}
'@
Save-ScenarioFile -ScenarioName "webhooks-create-snake-case" -FileName "request.json" `
    -Content (Format-Json $webhookSnakeBody)
$webhookSnakeResp = Invoke-Api -Method POST -Path "/webhooks" -ApiKey $webhookAuthKey -Body $webhookSnakeBody
Save-ScenarioFile -ScenarioName "webhooks-create-snake-case" -FileName "response.json" `
    -Content (Format-Json $webhookSnakeResp.Body)
if ($webhookSnakeResp.StatusCode -in @(200, 201)) {
    $wId = ($webhookSnakeResp.Body | ConvertFrom-Json -Depth 5).id
    if ($wId) {
        $whEsDoc = Get-EsDocument -IndexPattern "*-webhooks*" -DocId $wId
        Save-ScenarioFile -ScenarioName "webhooks-create-snake-case" -FileName "elastic.json" -Content $whEsDoc
    }
    Write-Host " ✓"
} else { Write-Host " ✗ ($($webhookSnakeResp.StatusCode))" }

# webhooks-create-camel-case
Write-Host "  → webhooks-create-camel-case" -NoNewline
$webhookCamelBody = @'
{
    "organizationId": "537650f3b77efe23a47914f3",
    "projectId": "537650f3b77efe23a47914f4",
    "url": "https://audit.localhost/webhook/camel",
    "eventTypes": ["NewError", "NewEvent"]
}
'@
Save-ScenarioFile -ScenarioName "webhooks-create-camel-case" -FileName "request.json" `
    -Content (Format-Json $webhookCamelBody)
$webhookCamelResp = Invoke-Api -Method POST -Path "/webhooks" -ApiKey $webhookAuthKey -Body $webhookCamelBody
Save-ScenarioFile -ScenarioName "webhooks-create-camel-case" -FileName "response.json" `
    -Content (Format-Json $webhookCamelResp.Body)
if ($webhookCamelResp.StatusCode -in @(200, 201)) {
    $wId2 = ($webhookCamelResp.Body | ConvertFrom-Json -Depth 5).id
    if ($wId2) {
        $whEsDoc2 = Get-EsDocument -IndexPattern "*-webhooks*" -DocId $wId2
        Save-ScenarioFile -ScenarioName "webhooks-create-camel-case" -FileName "elastic.json" -Content $whEsDoc2
    }
    Write-Host " ✓"
} else { Write-Host " ✗ ($($webhookCamelResp.StatusCode))" }

# DONE
Write-Host "`n[Done] Output saved to: $OutputDir`n"
