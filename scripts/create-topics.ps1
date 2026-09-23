$ErrorActionPreference = "Stop"

$topics = @(
    "mapping.file-import"
    "mapping.row-normalize"
)

$projectRoot = Split-Path -Parent $PSScriptRoot

Push-Location $projectRoot
try {
    foreach ($topic in $topics) {
        Write-Host "Ensuring Kafka topic '$topic' exists..."

        docker compose exec -T kafka /opt/kafka/bin/kafka-topics.sh `
            --bootstrap-server kafka:19092 `
            --create `
            --if-not-exists `
            --topic $topic `
            --partitions 3 `
            --replication-factor 1

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create Kafka topic '$topic'."
        }
    }
}
finally {
    Pop-Location
}
