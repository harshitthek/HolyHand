# ==============================================================================
# HolyHand Diagnostic - Connection & Credential Verifier
# ==============================================================================

$envFile = Join-Path $PSScriptRoot "..\.env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
            $parts = $line.Split("=", 2)
            $name = $parts[0].Trim()
            $value = $parts[1].Trim()
            if (-not [Environment]::GetEnvironmentVariable($name)) {
                [Environment]::SetEnvironmentVariable($name, $value)
            }
        }
    }
}

$apiKey = [Environment]::GetEnvironmentVariable("AI_GATEWAY_API_KEY")
if (-not $apiKey) {
    $apiKey = [Environment]::GetEnvironmentVariable("TYPESAFE_API_KEY")
}
if (-not $apiKey) {
    $apiKey = [Environment]::GetEnvironmentVariable("JEV_API_KEY")
}

if (-not $apiKey) {
    Write-Host "[ERROR] No API key found. Please set AI_GATEWAY_API_KEY in .env" -ForegroundColor Red
    exit 1
}

$baseUrl = [Environment]::GetEnvironmentVariable("AI_GATEWAY_BASE_URL")
if (-not $baseUrl) {
    if ($apiKey.StartsWith("apikey_")) {
        $baseUrl = "https://api.typesafe.ai"
    } else {
        $baseUrl = "https://ai-gateway.vercel.sh"
    }
}

$isNative = $baseUrl.Contains("typesafe.ai") -or $apiKey.StartsWith("apikey_")
$endpoint = if ($baseUrl.EndsWith("/systemone") -or $baseUrl.EndsWith("/evaluate")) {
    $baseUrl
} elseif ($isNative) {
    "$($baseUrl.TrimEnd('/'))/v1/systemone"
} else {
    "$($baseUrl.TrimEnd('/'))/v1/evaluate"
}

$model = [Environment]::GetEnvironmentVariable("JEV_MODEL")
if (-not $model) {
    $model = if ($isNative) { "jev-latest" } else { "typesafe-ai/jev" }
}

$redactedKey = if ($apiKey.Length -gt 8) {
    "$($apiKey.Substring(0, 4))...$($apiKey.Substring($apiKey.Length - 4))"
} else {
    "[CONFIGURED]"
}

Write-Host "HolyHand Jev Diagnostic Check" -ForegroundColor Cyan
Write-Host "--------------------------------------------------" -ForegroundColor Cyan
Write-Host "Provider:   $(if ($isNative) { 'TypeSafe Native API' } else { 'Vercel AI Gateway' })"
Write-Host "Endpoint:   $endpoint"
Write-Host "Model:      $model"
Write-Host "API Key:    $redactedKey"
Write-Host ""
Write-Host "Sending diagnostic request..." -NoNewline

$bodyObj = @{
    model = $model
    state = @{
        system = "HolyHand Windows Diagnostic"
        status = "Testing connectivity"
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
    }
    questions = @{
        operational = @{
            type = if ($isNative) { "noul" } else { "boolean" }
            instructions = "Is this connection active and ready for evaluation?"
        }
    }
}

if (-not $isNative) {
    $bodyObj["providerOptions"] = @{
        gateway = @{
            zeroDataRetention = $false
            only = @("typesafe-ai")
        }
    }
}

$jsonBody = $bodyObj | ConvertTo-Json -Depth 5
$headers = @{
    "Authorization" = "Bearer $apiKey"
    "Content-Type"  = "application/json"
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Headers $headers -Body $jsonBody
    $sw.Stop()

    Write-Host " OK ($($sw.ElapsedMilliseconds) ms)" -ForegroundColor Green
    Write-Host ""
    Write-Host "[SUCCESS] Connection and API credentials verified!" -ForegroundColor Green
    
    if ($response.answers.operational) {
        $ans = $response.answers.operational
        $prob = if ($ans.noul -ne $null) { $ans.noul } else { $ans.probability }
        Write-Host "  Response operational probability: $([math]::Round($prob * 100, 1))%"
    }
    if ($response.usage) {
        $u = $response.usage
        $prompt = if ($u.promptTokens) { $u.promptTokens } else { $u.input_tokens }
        $comp = if ($u.completionTokens) { $u.completionTokens } else { $u.output_tokens }
        Write-Host "  Usage tokens: Prompt=$prompt, Completion=$comp"
    }
    if ($response.model) {
        Write-Host "  Serving model: $($response.model)"
    }
    exit 0
} catch {
    $sw.Stop()
    Write-Host " FAILED ($($sw.ElapsedMilliseconds) ms)" -ForegroundColor Red
    Write-Host ""
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "  Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}
