# Patch the Easyer Launcher app registration via Microsoft Graph.
# Uses the Azure CLI's own credentials to acquire a Graph token (no extra consent prompt).
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$az     = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
$objId  = "838e9f73-7dcc-413e-ab1b-ba3f5c14e4ea"
$graph  = "https://graph.microsoft.com/v1.0"

Write-Host "=== acquire graph token ==="
$tok = & $az account get-access-token --resource-type ms-graph --output json 2>&1 | Out-String
if ($tok -notmatch '"accessToken"') { Write-Host $tok; throw "no token" }
$access = ($tok | ConvertFrom-Json).accessToken
$h = @{ Authorization = "Bearer $access" }

Write-Host "=== current state ==="
$before = Invoke-RestMethod -Headers $h -Uri "$graph/applications/$objId`?`$select=appId,displayName,signInAudience,isFallbackPublicClient,api,publicClient,web"
Write-Host ("  appId     : " + $before.appId)
Write-Host ("  name      : " + $before.displayName)
Write-Host ("  audience  : " + $before.signInAudience)
Write-Host ("  atVer     : " + $before.api.requestedAccessTokenVersion)
Write-Host ("  fallback  : " + $before.isFallbackPublicClient)
Write-Host ("  pubRedir  : " + ($before.publicClient.redirectUris -join ", "))
Write-Host ("  webRedir  : " + ($before.web.redirectUris -join ", "))

$body = @{
    signInAudience         = "AzureADandPersonalMicrosoftAccount"
    isFallbackPublicClient = $true
    api                    = @{ requestedAccessTokenVersion = 2 }
    publicClient           = @{ redirectUris = @("http://localhost/signin", "http://localhost:25566/signin") }
    web                    = @{ redirectUris = @("http://localhost/signin") }
} | ConvertTo-Json -Depth 5

Write-Host "=== PATCH ==="
try {
    $r = Invoke-RestMethod -Method Patch -Headers $h -ContentType "application/json" -Body $body -Uri "$graph/applications/$objId"
    Write-Host ("  audience : " + $r.signInAudience)
    Write-Host ("  atVer    : " + $r.api.requestedAccessTokenVersion)
    Write-Host ("  fallback : " + $r.isFallbackPublicClient)
    Write-Host ("  pubRedir : " + ($r.publicClient.redirectUris -join ", "))
    Write-Host "PATCH OK"
} catch {
    Write-Host "PATCH FAILED"
    Write-Host $_.ErrorDetails.Message
    throw
}
