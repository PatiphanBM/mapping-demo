param(
    [string] $OutputDirectory = (
        Join-Path (Split-Path -Parent $PSScriptRoot) "input/orders"
    )
)

$ErrorActionPreference = "Stop"

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null

$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
$header = "Order No,Customer Name,Order Date,Amount,Is Paid,Note"

function New-OrderLines {
    param(
        [Parameter(Mandatory)]
        [string] $Prefix,

        [Parameter(Mandatory)]
        [bool] $IncludeInvalidRows
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add($header)

    for ($rowNumber = 1; $rowNumber -le 100; $rowNumber++) {
        $day = (($rowNumber - 1) % 28) + 1
        $orderDate = "2026-01-{0:D2}" -f $day
        $amount = "{0}.50" -f (100 + $rowNumber)

        if ($IncludeInvalidRows -and $rowNumber -eq 25) {
            $orderDate = "2026-02-30"
        }

        if ($IncludeInvalidRows -and $rowNumber -eq 75) {
            $amount = "not-a-number"
        }

        $isPaid = if ($rowNumber % 2 -eq 0) { "true" } else { "false" }
        $line = (
            "{0}-{1:D3},Customer {0}-{1:D3},{2},{3},{4},Sample row {1}" -f `
            $Prefix,
            $rowNumber,
            $orderDate,
            $amount,
            $isPaid
        )
        $lines.Add($line)
    }

    return $lines
}

$ordersAPath = Join-Path $outputPath "orders-a.csv"
$ordersBPath = Join-Path $outputPath "orders-b.csv"

[System.IO.File]::WriteAllLines(
    $ordersAPath,
    (New-OrderLines -Prefix "A" -IncludeInvalidRows $true),
    $utf8WithoutBom
)
[System.IO.File]::WriteAllLines(
    $ordersBPath,
    (New-OrderLines -Prefix "B" -IncludeInvalidRows $false),
    $utf8WithoutBom
)

Write-Host "Created sample files:"
Write-Host "  $ordersAPath (100 records; invalid data rows: 25 and 75)"
Write-Host "  $ordersBPath (100 valid records)"
