param(
    [string] $ApiBaseUrl = "http://localhost:5181",
    [long] $ConfigId = 1
)

$ErrorActionPreference = "Stop"
$apiBaseUrl = $ApiBaseUrl.TrimEnd("/")

function New-Version {
    param(
        [Parameter(Mandatory)]
        [string] $DateFormat
    )

    $fileToSource = @(
        @{ csvHeader = "Order No"; sourceColumn = "order_no" }
        @{ csvHeader = "Customer Name"; sourceColumn = "customer_name" }
        @{ csvHeader = "Order Date"; sourceColumn = "order_date" }
        @{ csvHeader = "Amount"; sourceColumn = "amount" }
        @{ csvHeader = "Is Paid"; sourceColumn = "is_paid" }
        @{ csvHeader = "Note"; sourceColumn = "note" }
    )
    $sourceToNormalized = @(
        @{ sourceColumn = "order_no"; normalizedColumn = "order_no" }
        @{ sourceColumn = "customer_name"; normalizedColumn = "customer_name" }
        @{
            sourceColumn = "order_date"
            normalizedColumn = "order_date"
            format = $DateFormat
        }
        @{ sourceColumn = "amount"; normalizedColumn = "amount" }
        @{ sourceColumn = "is_paid"; normalizedColumn = "is_paid" }
        @{ sourceColumn = "note"; normalizedColumn = "note" }
    )
    $body = @{
        fileToSource = $fileToSource
        sourceToNormalized = $sourceToNormalized
    } | ConvertTo-Json -Depth 10 -Compress

    return Invoke-RestMethod `
        -Method Post `
        -Uri "$apiBaseUrl/mapping-configs/$ConfigId/versions" `
        -ContentType "application/json" `
        -Body $body
}

Write-Host "Checking mapping config $ConfigId..."
$config = Invoke-RestMethod `
    -Method Get `
    -Uri "$apiBaseUrl/mapping-configs/$ConfigId"

if ($config.versions.Count -ne 1) {
    throw (
        "Expected exactly one version after configure-demo.ps1, but found {0}. " +
        "Reset the demo before running this script." -f $config.versions.Count
    )
}

$version2 = New-Version -DateFormat "dd/MM/yyyy"
$version3 = New-Version -DateFormat "MM/dd/yyyy"

Write-Host "Created acceptance-test versions (neither was activated):"
Write-Host (
    "  Version {0}: ID {1}, date format dd/MM/yyyy (expected success)" -f `
        $version2.versionNo,
        $version2.id
)
Write-Host (
    "  Version {0}: ID {1}, date format MM/dd/yyyy (expected invalid)" -f `
        $version3.versionNo,
        $version3.id
)
