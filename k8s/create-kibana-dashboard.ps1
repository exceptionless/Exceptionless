# Creates the Exceptionless metrics dashboard in Kibana via the Saved Objects API.
# This bypasses the import/migration pipeline that causes transform errors.
#
# Prerequisites:
#   kubectl port-forward -n elastic-system svc/elastic-monitor-kb-http 5601
#   $env:KIBANA_PASSWORD = kubectl get secret elastic-monitor-password -n elastic-system -o jsonpath='{.data.password}' | % { [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($_)) }
#
# Usage:
#   ./create-kibana-dashboard.ps1

param(
    [string]$KibanaUrl = "https://kibana.exceptionless.io",
    [string]$User = "monitor-admin",
    [string]$Password = $env:KIBANA_PASSWORD
)

$ErrorActionPreference = "Stop"

if (-not $Password) {
    Write-Error "Set `$env:KIBANA_PASSWORD or pass -Password"
    exit 1
}

$headers = @{
    "kbn-xsrf" = "true"
    "Content-Type" = "application/json"
}

$cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${User}:${Password}"))
$headers["Authorization"] = "Basic $cred"

# --- 1. Create data view ---
Write-Host "Creating data view 'metrics-apm.app*'..."
$dataViewBody = @{
    attributes = @{
        title = "metrics-apm.app*"
        timeFieldName = "@timestamp"
    }
} | ConvertTo-Json -Depth 5

try {
    Invoke-RestMethod -Uri "$KibanaUrl/api/saved_objects/index-pattern/metrics-apm-data-view?overwrite=true" `
        -Method POST -Headers $headers -Body $dataViewBody -SkipCertificateCheck
    Write-Host "  Data view created." -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "  Data view already exists, skipping." -ForegroundColor Yellow
    } else { throw }
}

# --- 2. Build dashboard panels ---
function New-MetricPanel($id, $x, $y, $title, $field, $op, $params) {
    # For counter fields, use max - min to get total increase over the time range.
    # For "formula" op, use params.formula directly with explicit sub-columns.
    # For histogram fields, use direct operation.
    $isCounter = $op -eq "counter_rate"
    $isFormula = $op -eq "formula"

    if ($isCounter) {
        $columns = [ordered]@{
            c1_max = @{ label = ""; dataType = "number"; operationType = "max"; sourceField = $field; isBucketed = $false }
            c1_min = @{ label = ""; dataType = "number"; operationType = "min"; sourceField = $field; isBucketed = $false }
            c1 = @{
                label = $title
                customLabel = $true
                dataType = "number"
                operationType = "math"
                isBucketed = $false
                params = @{ tinymathAst = @{ type = "function"; name = "subtract"; args = @("c1_max", "c1_min") } }
                references = @("c1_max", "c1_min")
            }
        }
        $columnOrder = @("c1_max", "c1_min", "c1")
    } elseif ($isFormula) {
        # Queue time: percentile / 1000 for seconds display
        $columns = [ordered]@{
            c1_raw = @{ label = ""; dataType = "number"; operationType = "percentile"; sourceField = $field; isBucketed = $false; params = $params.subParams }
            c1 = @{
                label = $title
                customLabel = $true
                dataType = "number"
                operationType = "math"
                isBucketed = $false
                params = @{ tinymathAst = @{ type = "function"; name = "divide"; args = @("c1_raw", 1000) } }
                references = @("c1_raw")
            }
        }
        $columnOrder = @("c1_raw", "c1")
    } else {
        $col = @{
            label = $title
            customLabel = $true
            dataType = "number"
            operationType = $op
            sourceField = $field
            isBucketed = $false
        }
        if ($params) { $col.params = $params }
        $columns = [ordered]@{ c1 = $col }
        $columnOrder = @("c1")
    }

    return @{
        type = "lens"
        gridData = @{ x = $x; y = $y; w = 8; h = 6; i = $id }
        panelIndex = $id
        embeddableConfig = @{
            title = ""
            hidePanelTitles = $true
            attributes = @{
                title = ""
                visualizationType = "lnsMetric"
                type = "lens"
                state = @{
                    datasourceStates = @{
                        formBased = @{
                            layers = @{
                                l1 = @{
                                    columns = $columns
                                    columnOrder = $columnOrder
                                    incompleteColumns = @{}
                                }
                            }
                        }
                    }
                    visualization = @{
                        layerId = "l1"
                        layerType = "data"
                        metricAccessor = "c1"
                    }
                    query = @{ query = ""; language = "kuery" }
                    filters = @()
                    adHocDataViews = @{}
                }
                references = @(
                    @{ type = "index-pattern"; id = "metrics-apm-data-view"; name = "indexpattern-datasource-layer-l1" }
                )
            }
        }
    }
}

function New-XYPanel($id, $x, $y, $w, $h, $title, $columns, $accessors) {
    # For counter_rate columns, expand into max + differences reference pair
    $expandedColumns = [ordered]@{}
    $expandedOrder = @("ts")
    $expandedAccessors = @()

    foreach ($key in $columns.Keys) {
        $col = $columns[$key]
        if ($key -eq "ts") {
            $expandedColumns["ts"] = $col
        } elseif ($col.operationType -eq "counter_rate") {
            # Create a max column as the base
            $rawKey = "${key}_raw"
            $expandedColumns[$rawKey] = @{
                label = "$($col.label) raw"
                dataType = "number"
                operationType = "max"
                sourceField = $col.sourceField
                isBucketed = $false
            }
            # Create a counter_rate column referencing the max (handles resets gracefully)
            $expandedColumns[$key] = @{
                label = $col.label
                customLabel = $true
                dataType = "number"
                operationType = "counter_rate"
                isBucketed = $false
                references = @($rawKey)
            }
            $expandedOrder += $rawKey
            $expandedOrder += $key
            $expandedAccessors += $key
        } else {
            $expandedColumns[$key] = $col
            $expandedOrder += $key
            $expandedAccessors += $key
        }
    }

    return @{
        type = "lens"
        gridData = @{ x = $x; y = $y; w = $w; h = $h; i = $id }
        panelIndex = $id
        embeddableConfig = @{
            title = $title
            attributes = @{
                title = ""
                visualizationType = "lnsXY"
                type = "lens"
                state = @{
                    datasourceStates = @{
                        formBased = @{
                            layers = @{
                                l1 = @{
                                    columns = $expandedColumns
                                    columnOrder = $expandedOrder
                                    incompleteColumns = @{}
                                }
                            }
                        }
                    }
                    visualization = @{
                        legend = @{ isVisible = $true; position = "right" }
                        preferredSeriesType = "line"
                        layers = @(
                            @{
                                layerId = "l1"
                                accessors = $expandedAccessors
                                seriesType = "line"
                                xAccessor = "ts"
                                layerType = "data"
                            }
                        )
                    }
                    query = @{ query = ""; language = "kuery" }
                    filters = @()
                    adHocDataViews = @{}
                }
                references = @(
                    @{ type = "index-pattern"; id = "metrics-apm-data-view"; name = "indexpattern-datasource-layer-l1" }
                )
            }
        }
    }
}

$ts = @{ label = "@timestamp"; dataType = "date"; operationType = "date_histogram"; sourceField = "@timestamp"; isBucketed = $true; params = @{ interval = "auto" } }

$panels = @(
    # KPI row
    (New-MetricPanel "p1" 0  0 "P99 Queue Time (s)" "foundatio.eventpost.queuetime" "formula" @{ subParams = @{ percentile = 99 } })
    (New-MetricPanel "p2" 8  0 "P95 Queue Time (s)" "foundatio.eventpost.queuetime" "formula" @{ subParams = @{ percentile = 95 } })
    (New-MetricPanel "p3" 16 0 "Submitted"           "ex.events.submitted"            "counter_rate" $null)
    (New-MetricPanel "p4" 24 0 "Processed"           "ex.events.all.processed"       "counter_rate" $null)
    (New-MetricPanel "p5" 32 0 "Blocked"             "ex.events.blocked"              "counter_rate" $null)
    (New-MetricPanel "p6" 40 0 "Discarded"           "ex.events.discarded"            "counter_rate" $null)

    # Events graph
    (New-XYPanel "p7" 0 6 48 14 "Events" ([ordered]@{
        ts        = $ts
        submitted = @{ label = "Submitted"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.events.submitted" }
        processed = @{ label = "Processed"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.events.all.processed" }
        blocked   = @{ label = "Blocked";   customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.events.blocked" }
        discarded = @{ label = "Discarded"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.events.discarded" }
    }) @("submitted","processed","blocked","discarded"))

    # Queue Counts
    (New-XYPanel "p8" 0 20 48 14 "Queue Counts" ([ordered]@{
        ts            = $ts
        events        = @{ label = "Events";        customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "max"; sourceField = "foundatio.eventpost.count" }
        notifications = @{ label = "Notifications"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "max"; sourceField = "foundatio.eventnotification.count" }
        mail          = @{ label = "Mail";          customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "max"; sourceField = "foundatio.mailmessage.count" }
        work          = @{ label = "Work Items";    customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "max"; sourceField = "foundatio.workitemdata.count" }
    }) @("events","notifications","mail","work"))

    # Event Processing Time
    (New-XYPanel "p9" 0 34 24 14 "Event Processing Time" ([ordered]@{
        ts  = $ts
        avg = @{ label = "Avg"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "average"; sourceField = "ex.events.processingtime" }
        p95 = @{ label = "P95"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.events.processingtime"; params = @{ percentile = 95 } }
        p99 = @{ label = "P99"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.events.processingtime"; params = @{ percentile = 99 } }
    }) @("avg","p95","p99"))

    # Post Parsing Time
    (New-XYPanel "p10" 24 34 24 14 "Post Parsing Time" ([ordered]@{
        ts  = $ts
        avg = @{ label = "Avg"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "average"; sourceField = "ex.posts.parsingtime" }
        p95 = @{ label = "P95"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.posts.parsingtime"; params = @{ percentile = 95 } }
    }) @("avg","p95"))

    # Cancellations & Blocks
    (New-XYPanel "p11" 0 48 24 14 "Cancellations & Blocks" ([ordered]@{
        ts        = $ts
        cancelled = @{ label = "Cancelled"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.events.processing.cancelled" }
        blocked   = @{ label = "Blocked";   customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.events.blocked" }
        discarded = @{ label = "Discarded"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.events.discarded" }
    }) @("cancelled","blocked","discarded"))

    # Post Size
    (New-XYPanel "p12" 24 48 24 14 "Post Size (bytes)" ([ordered]@{
        ts       = $ts
        uncomp   = @{ label = "Uncompressed Avg"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "average";    sourceField = "ex.posts.uncompressed.size" }
        comp     = @{ label = "Compressed Avg";   customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "average";    sourceField = "ex.posts.compressed.size" }
        uncomp95 = @{ label = "Uncompressed P95"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.posts.uncompressed.size"; params = @{ percentile = 95 } }
    }) @("uncomp","comp","uncomp95"))

    # Events Per Post
    (New-XYPanel "p13" 0 62 24 14 "Events Per Post" ([ordered]@{
        ts  = $ts
        avg = @{ label = "Avg"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "average";    sourceField = "ex.posts.eventcount" }
        p95 = @{ label = "P95"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.posts.eventcount"; params = @{ percentile = 95 } }
    }) @("avg","p95"))

    # Mailer Throughput
    (New-XYPanel "p14" 24 62 24 14 "Mailer Throughput" ([ordered]@{
        ts      = $ts
        event   = @{ label = "Event Notice";   customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.mailer.event-notice" }
        daily   = @{ label = "Daily Summary";  customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.mailer.project-daily-summary" }
        org     = @{ label = "Org Notice";     customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.mailer.organization-notice" }
        payment = @{ label = "Payment Failed"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.mailer.organization-payment-failed" }
        invited = @{ label = "Invited";        customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "counter_rate"; sourceField = "ex.mailer.organization-invited" }
    }) @("event","daily","org","payment","invited"))

    # Pipeline Action Times (P95)
    (New-XYPanel "p15" 0 76 48 16 "Pipeline Action Times P95 (ms)" ([ordered]@{
        ts         = $ts
        assign     = @{ label = "AssignToStack";     customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.eventpipeline.assigntostackaction";              params = @{ percentile = 95 } }
        save       = @{ label = "SaveEvent";         customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.eventpipeline.saveeventaction";                  params = @{ percentile = 95 } }
        plugins    = @{ label = "RunPlugins";        customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.eventpipeline.runeventprocessingpluginsaction";   params = @{ percentile = 95 } }
        stats      = @{ label = "UpdateStats";       customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.eventpipeline.updatestatsaction";                params = @{ percentile = 95 } }
        regression = @{ label = "CheckRegression";   customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.eventpipeline.checkforregressionaction";         params = @{ percentile = 95 } }
        notify     = @{ label = "QueueNotification"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.eventpipeline.queuenotificationaction";          params = @{ percentile = 95 } }
        copy       = @{ label = "CopyToIdx";         customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.eventpipeline.copysimpledatatoidxaction";        params = @{ percentile = 95 } }
        counters   = @{ label = "IncrementCounters"; customLabel = $true; isBucketed = $false; dataType = "number"; operationType = "percentile"; sourceField = "ex.eventpipeline.incrementcountersaction";          params = @{ percentile = 95 } }
    }) @("assign","save","plugins","stats","regression","notify","copy","counters"))
)

# Build references array
$references = $panels | ForEach-Object {
    @{ type = "index-pattern"; id = "metrics-apm-data-view"; name = "$($_.panelIndex):indexpattern-datasource-layer-l1" }
}

$dashboardBody = @{
    attributes = @{
        title = "Exceptionless"
        description = "Exceptionless application metrics - event processing, queues, and pipeline performance"
        panelsJSON = ($panels | ConvertTo-Json -Depth 20 -Compress)
        kibanaSavedObjectMeta = @{
            searchSourceJSON = '{"query":{"query":"","language":"kuery"},"filter":[]}'
        }
        timeRestore = $true
        timeTo = "now"
        timeFrom = "now-1h"
        refreshInterval = @{ pause = $false; value = 30000 }
        version = 1
    }
    references = $references
} | ConvertTo-Json -Depth 25

# --- 3. Create dashboard ---
Write-Host "Creating dashboard 'Exceptionless'..."
try {
    Invoke-RestMethod -Uri "$KibanaUrl/api/saved_objects/dashboard/exceptionless-dashboard?overwrite=true" `
        -Method POST -Headers $headers -Body $dashboardBody -SkipCertificateCheck
    Write-Host "  Dashboard created." -ForegroundColor Green
} catch {
    $status = $_.Exception.Response.StatusCode
    $detail = $_.ErrorDetails.Message
    Write-Error "Failed ($status): $detail"
    exit 1
}

Write-Host "`nDone! Open: $KibanaUrl/app/dashboards#/view/exceptionless-dashboard" -ForegroundColor Cyan
