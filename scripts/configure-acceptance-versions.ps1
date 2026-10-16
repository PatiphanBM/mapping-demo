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
                        value = [ordered]@{
                            kind = "source"
                            field = "customer_name"
                        }
                    }
                    @{
                        normalizedColumn = "order_date"
                        inputFormat = $DateFormat
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
    $body = @{
        inputLayout = $inputLayout
        transformationProgram = $transformationProgram
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
$version3 = New-Version -DateFormat "yyyyMMdd"

Write-Host "Created acceptance-test versions (neither was activated):"
Write-Host (
    "  Version {0}: ID {1}, date format dd/MM/yyyy (expected success)" -f `
        $version2.versionNo,
        $version2.id
)
Write-Host (
    "  Version {0}: ID {1}, date format yyyyMMdd (expected invalid)" -f `
        $version3.versionNo,
        $version3.id
)
