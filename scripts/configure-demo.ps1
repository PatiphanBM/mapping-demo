param(
    [string] $ApiBaseUrl = "http://localhost:5181"
)

$ErrorActionPreference = "Stop"

$apiBaseUrl = $ApiBaseUrl.TrimEnd("/")

function Invoke-DemoPost {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [object] $Body
    )

    return Invoke-RestMethod `
        -Method Post `
        -Uri "$apiBaseUrl$Path" `
        -ContentType "application/json" `
        -Body ($Body | ConvertTo-Json -Depth 10 -Compress)
}

Write-Host "Checking API at $apiBaseUrl..."
Invoke-RestMethod -Method Get -Uri "$apiBaseUrl/health" | Out-Null

$sourceColumns = @(
    @{ name = "order_no"; dataType = 0; isRequired = $false }
    @{ name = "customer_name"; dataType = 0; isRequired = $false }
    @{ name = "order_date"; dataType = 0; isRequired = $false }
    @{ name = "amount"; dataType = 0; isRequired = $false }
    @{ name = "is_paid"; dataType = 0; isRequired = $false }
    @{ name = "note"; dataType = 0; isRequired = $false }
)
$normalizedColumns = @(
    @{ name = "order_no"; dataType = 0; isRequired = $true }
    @{ name = "customer_name"; dataType = 0; isRequired = $true }
    @{ name = "order_date"; dataType = 1; isRequired = $true }
    @{ name = "amount"; dataType = 2; isRequired = $true }
    @{ name = "is_paid"; dataType = 3; isRequired = $true }
    @{ name = "note"; dataType = 0; isRequired = $false }
)

Write-Host "Creating Source and Normalized tables through the API..."
$sourceTable = Invoke-DemoPost -Path "/tables" -Body @{
    name = "source_orders"
    kind = 0
    columns = $sourceColumns
}
$normalizedTable = Invoke-DemoPost -Path "/tables" -Body @{
    name = "normalized_orders"
    kind = 1
    columns = $normalizedColumns
}

Write-Host "Creating and activating the mapping config..."
$config = Invoke-DemoPost -Path "/mapping-configs" -Body @{
    name = "orders-demo"
    inputFolder = "orders"
    sourceTableId = $sourceTable.id
    normalizedTableId = $normalizedTable.id
}

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
        format = "yyyy-MM-dd"
    }
    @{ sourceColumn = "amount"; normalizedColumn = "amount" }
    @{ sourceColumn = "is_paid"; normalizedColumn = "is_paid" }
    @{ sourceColumn = "note"; normalizedColumn = "note" }
)

$version = Invoke-DemoPost `
    -Path "/mapping-configs/$($config.id)/versions" `
    -Body @{
        fileToSource = $fileToSource
        sourceToNormalized = $sourceToNormalized
    }

Invoke-RestMethod `
    -Method Post `
    -Uri "$apiBaseUrl/mapping-configs/$($config.id)/versions/$($version.versionNo)/activate" |
    Out-Null

Write-Host (
    "Demo config ready: config ID {0}, version {1}, input/orders." -f `
        $config.id,
        $version.versionNo
)
