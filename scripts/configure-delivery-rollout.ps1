param(
    [string] $ApiBaseUrl = "http://localhost:5181",
    [Parameter(Mandatory)]
    [long] $ConfigId,
    [ValidateSet("Shadow", "Canary", "Expanded")]
    [string] $Stage = "Shadow",
    [string] $OperatorId = "local-operator",
    [string] $Reason = "Local delivery rollout",
    [switch] $Pause,
    [switch] $DisableApiActivation,
    [switch] $EnableJsonFileActivation,
    [switch] $DisableJsonFileActivation,
    [switch] $ResumeJsonFileFinalizer,
    [switch] $PauseJsonFileFinalizer
)

$ErrorActionPreference = "Stop"
$apiBaseUrl = $ApiBaseUrl.TrimEnd("/")
$headers = @{ "X-Operator-Id" = $OperatorId }

if ($EnableJsonFileActivation -and $DisableJsonFileActivation) {
    throw "Choose either EnableJsonFileActivation or DisableJsonFileActivation."
}
if ($ResumeJsonFileFinalizer -and $PauseJsonFileFinalizer) {
    throw "Choose either ResumeJsonFileFinalizer or PauseJsonFileFinalizer."
}

Invoke-RestMethod `
    -Method Put `
    -Uri "$apiBaseUrl/delivery-operations/rollouts/$ConfigId" `
    -Headers $headers `
    -ContentType "application/json" `
    -Body (@{
        stage = $Stage
        enabled = $true
        reason = $Reason
    } | ConvertTo-Json -Compress) | Out-Null

$controlBody = [ordered]@{
    deliveryPaused = $Pause.IsPresent
    apiContractActivationEnabled = -not $DisableApiActivation.IsPresent
    reason = $Reason
}
if ($EnableJsonFileActivation) {
    $controlBody["jsonFileActivationEnabled"] = $true
}
elseif ($DisableJsonFileActivation) {
    $controlBody["jsonFileActivationEnabled"] = $false
}
if ($ResumeJsonFileFinalizer) {
    $controlBody["jsonFileFinalizerPaused"] = $false
}
elseif ($PauseJsonFileFinalizer) {
    $controlBody["jsonFileFinalizerPaused"] = $true
}

$control = Invoke-RestMethod `
    -Method Put `
    -Uri "$apiBaseUrl/delivery-operations/control" `
    -Headers $headers `
    -ContentType "application/json" `
    -Body ($controlBody | ConvertTo-Json -Compress)

Write-Host (
    "Config {0}: stage={1}, enabled=true; deliveryPaused={2}; apiActivation={3}; jsonActivation={4}; jsonFinalizerPaused={5}" -f `
        $ConfigId,
        $Stage,
        $control.deliveryPaused,
        $control.apiContractActivationEnabled,
        $control.jsonFileActivationEnabled,
        $control.jsonFileFinalizerPaused
)
