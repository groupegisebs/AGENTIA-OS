# Synchronise le catalogue Pay Gateway pour AGENTIAOS (plans Starter / Pro / Enterprise).
# Prérequis : application AGENTIAOS créée dans l'admin Pay Gateway + clé API valide.
#
# Usage :
#   .\deploy\sync-paygateway-catalog.ps1
#   .\deploy\sync-paygateway-catalog.ps1 -ApiKey "gbsk_..." -BaseUrl "https://gisebsapipaygateway.gisebs.com"

param(
    [string]$BaseUrl = "https://gisebsapipaygateway.gisebs.com",
    [string]$AppCode = "AGENTIAOS",
    [string]$ApiKey = $env:GISEBS_PAYGATEWAY_API_KEY,
    [string]$PlanCode = "MONTHLY"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Error "ApiKey requis. Passez -ApiKey ou définissez GISEBS_PAYGATEWAY_API_KEY."
}

$products = @(
    @{ ProductCode = "AGENTIA-STARTER";    Name = "AGENTIA-OS Starter";    Amount = 99;  Description = "20 agents, 5 000 runs/mois" },
    @{ ProductCode = "AGENTIA-PRO";       Name = "AGENTIA-OS Pro";       Amount = 299; Description = "100 agents, 50 000 runs/mois" },
    @{ ProductCode = "AGENTIA-ENTERPRISE"; Name = "AGENTIA-OS Enterprise"; Amount = 999; Description = "500 agents, 500 000 runs/mois" }
)

Write-Host "Test authentification Pay Gateway ($AppCode)..." -ForegroundColor Cyan
$test = curl.exe -s -w "`nHTTP:%{http_code}" `
    -H "X-App-Code: $AppCode" `
    -H "X-Api-Key: $ApiKey" `
    "$BaseUrl/api/products"

if ($test -match 'Application cliente invalide') {
    Write-Error "Application $AppCode introuvable dans Pay Gateway. Créez-la dans l'admin (voir deploy/PAYGATEWAY-CATALOG.md)."
}
if ($test -match 'API Key invalide') {
    Write-Error "Clé API invalide pour $AppCode."
}
Write-Host "Authentification OK." -ForegroundColor Green

foreach ($p in $products) {
    $body = @{
        productCode   = $p.ProductCode
        productName   = $p.Name
        description   = $p.Description
        planCode      = $PlanCode
        planName      = "Mensuel"
        amount        = $p.Amount
        currency      = "USD"
        syncToStripe  = $true
    } | ConvertTo-Json -Compress

    Write-Host "Création catalogue $($p.ProductCode) / $PlanCode..." -ForegroundColor Cyan
    $response = curl.exe -s -w "`nHTTP:%{http_code}" `
        -X POST `
        -H "X-App-Code: $AppCode" `
        -H "X-Api-Key: $ApiKey" `
        -H "Content-Type: application/json" `
        -d $body `
        "$BaseUrl/api/products/catalog"

    if ($response -match 'HTTP:201' -or $response -match 'HTTP:200') {
        Write-Host "  OK" -ForegroundColor Green
    }
    elseif ($response -match 'exist|déjà|deja|duplicate') {
        Write-Host "  Déjà présent — ignoré." -ForegroundColor Yellow
    }
    else {
        Write-Warning "  Réponse : $response"
    }
}

Write-Host "Terminé." -ForegroundColor Green
