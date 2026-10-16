param(
    [string] $ApiBaseUrl = "http://localhost:5181"
)

$ErrorActionPreference = "Stop"

$apiBaseUrl = $ApiBaseUrl.TrimEnd("/")
$projectRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent $PSScriptRoot)
)

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

function Invoke-TrustedDemoSql {
    param(
        [Parameter(Mandatory)]
        [string] $Sql
    )

    Push-Location $projectRoot
    try {
        docker compose exec -T postgres `
            psql -U mapping -d mapping -v ON_ERROR_STOP=1 -c $Sql
        if ($LASTEXITCODE -ne 0) {
            throw "Trusted demo SQL failed."
        }
    }
    finally {
        Pop-Location
    }
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

$inputLayout = [ordered]@{
    kind = "delimited"
    encoding = "utf-8"
    delimiter = ","
    preambleLines = 0
    headerPolicy = "SingleRow"
    fields = @(
        @{ inputHeader = "Order No"; sourceColumn = "order_no" }
        @{ inputHeader = "Customer Name"; sourceColumn = "customer_name" }
        @{ inputHeader = "Order Date"; sourceColumn = "order_date" }
        @{ inputHeader = "Amount"; sourceColumn = "amount" }
        @{ inputHeader = "Is Paid"; sourceColumn = "is_paid" }
        @{ inputHeader = "Note"; sourceColumn = "note" }
    )
}
$transformationProgram = @{
    languageVersion = 1
    semanticsProfile = "sqlserver-legacy-v1"
    outputs = @(
        @{
            outputKey = "default"
            fields = @(
                @{
                    normalizedColumn = "order_no"
                    value = [ordered]@{ kind = "source"; field = "order_no" }
                }
                @{
                    normalizedColumn = "customer_name"
                    value = [ordered]@{ kind = "source"; field = "customer_name" }
                }
                @{
                    normalizedColumn = "order_date"
                    inputFormat = "yyyy-MM-dd"
                    value = [ordered]@{ kind = "source"; field = "order_date" }
                }
                @{
                    normalizedColumn = "amount"
                    value = [ordered]@{ kind = "source"; field = "amount" }
                }
                @{
                    normalizedColumn = "is_paid"
                    value = [ordered]@{ kind = "source"; field = "is_paid" }
                }
                @{
                    normalizedColumn = "note"
                    value = [ordered]@{ kind = "source"; field = "note" }
                }
            )
        }
    )
}

$version = Invoke-DemoPost `
    -Path "/mapping-configs/$($config.id)/versions" `
    -Body @{
        inputLayout = $inputLayout
        transformationProgram = $transformationProgram
    }

Invoke-RestMethod `
    -Method Post `
    -Uri "$apiBaseUrl/mapping-configs/$($config.id)/versions/$($version.versionNo)/activate" |
    Out-Null

$pmibSourceColumns = @(
    @{ name = "system_id"; dataType = 0; isRequired = $false }
    @{ name = "product_name"; dataType = 0; isRequired = $false }
    @{ name = "loan_number"; dataType = 0; isRequired = $false }
)
$pmibNormalizedColumns = @(
    @{ name = "system_id"; dataType = 0; isRequired = $true }
    @{ name = "cproductname"; dataType = 0; isRequired = $true }
    @{ name = "cloannumber"; dataType = 0; isRequired = $true }
)

Write-Host "Creating PMIB Source and MTI_ORIGINAL tables through the API..."
$pmibSourceTable = Invoke-DemoPost -Path "/tables" -Body @{
    name = "source_pmib"
    kind = 0
    columns = $pmibSourceColumns
}
$mtiOriginalTable = Invoke-DemoPost -Path "/tables" -Body @{
    name = "mti_original"
    kind = 1
    columns = $pmibNormalizedColumns
}

Write-Host "Creating the trusted MTI_ORIGINAL lookup index..."
Invoke-TrustedDemoSql -Sql (
    'CREATE INDEX ix_mti_original_policy_lookup ON "mti_original" ' +
    '("system_id", "cproductname", "cloannumber");'
)

$pmibConfig = Invoke-DemoPost -Path "/mapping-configs" -Body @{
    name = "pmib-demo"
    inputFolder = "pmib"
    sourceTableId = $pmibSourceTable.id
    normalizedTableId = $mtiOriginalTable.id
}
$pmibInputLayout = [ordered]@{
    kind = "delimited"
    encoding = "utf-8"
    delimiter = ","
    preambleLines = 0
    headerPolicy = "SingleRow"
    fields = @(
        @{ inputHeader = "System ID"; sourceColumn = "system_id" }
        @{ inputHeader = "Product Name"; sourceColumn = "product_name" }
        @{ inputHeader = "Loan Number"; sourceColumn = "loan_number" }
    )
}
$pmibTransformationProgram = @{
    languageVersion = 1
    semanticsProfile = "sqlserver-legacy-v1"
    outputs = @(
        @{
            outputKey = "default"
            emitWhen = [ordered]@{
                kind = "notExists"
                lookup = "mtiOriginalPolicyExists@1"
                arguments = @{
                    systemId = [ordered]@{ kind = "source"; field = "system_id" }
                    productName = [ordered]@{ kind = "source"; field = "product_name" }
                    loanNumber = [ordered]@{ kind = "source"; field = "loan_number" }
                }
            }
            fields = @(
                @{
                    normalizedColumn = "system_id"
                    value = [ordered]@{ kind = "source"; field = "system_id" }
                }
                @{
                    normalizedColumn = "cproductname"
                    value = [ordered]@{ kind = "source"; field = "product_name" }
                }
                @{
                    normalizedColumn = "cloannumber"
                    value = [ordered]@{ kind = "source"; field = "loan_number" }
                }
            )
        }
    )
}

$pmibVersion = Invoke-DemoPost `
    -Path "/mapping-configs/$($pmibConfig.id)/versions" `
    -Body @{
        inputLayout = $pmibInputLayout
        transformationProgram = $pmibTransformationProgram
    }

Invoke-RestMethod `
    -Method Post `
    -Uri "$apiBaseUrl/mapping-configs/$($pmibConfig.id)/versions/$($pmibVersion.versionNo)/activate" |
    Out-Null

Write-Host (
    "Orders demo ready: config ID {0}, version {1}, input/orders." -f `
        $config.id,
        $version.versionNo
)
Write-Host (
    "PMIB demo ready: config ID {0}, version {1}, input/pmib." -f `
        $pmibConfig.id,
        $pmibVersion.versionNo
)
