$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent $PSScriptRoot)
)
$runtimeDirectoryNames = @("input", "archive", "data")
$runtimeDirectories = foreach ($directoryName in $runtimeDirectoryNames) {
    $path = [System.IO.Path]::GetFullPath(
        (Join-Path $projectRoot $directoryName)
    )
    $parent = [System.IO.Path]::GetDirectoryName($path)

    if (-not [string]::Equals(
        $parent,
        $projectRoot,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        throw "Refusing to clear path outside the project root: $path"
    }

    $path
}

Push-Location $projectRoot
try {
    Write-Host "Stopping containers and removing demo volumes..."
    docker compose down -v
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose down -v failed."
    }

    foreach ($directory in $runtimeDirectories) {
        if (Test-Path -LiteralPath $directory) {
            Write-Host "Clearing $directory..."
            Remove-Item -LiteralPath $directory -Recurse -Force
        }

        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    Write-Host "Starting PostgreSQL, Kafka, and Kafka UI..."
    docker compose up -d --wait
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up -d --wait failed."
    }

    & (Join-Path $PSScriptRoot "create-topics.ps1")
    Write-Host "Demo state reset successfully."
}
finally {
    Pop-Location
}
