# 一键修好 Azure 应用的「个人账户登录」配置（不依赖 Azure 门户网页）
#
# 背景：启动器报 unauthorized_client ... not enabled for consumers，
# 根因是应用的「受支持的账户类型」只允许本组织目录，且未开启公共客户端流。
# 门户的 Authentication (Preview) 页面经常无限转圈，本脚本改用 Microsoft Graph 直接 PATCH。
#
# 用法（PowerShell，管理员或普通均可）：
#   powershell -ExecutionPolicy Bypass -File tools\fix-azure-app.ps1
#   powershell -ExecutionPolicy Bypass -File tools\fix-azure-app.ps1 -AppId "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
#
# 脚本会：
#   1) 走设备码流程，让你在浏览器里用「注册应用的那个微软账号」登录一次
#   2) 读取应用的 objectId
#   3) PATCH 三项：signInAudience / publicClient.redirectUris / isFallbackPublicClient
#   4) 回读校验并打印结果

param(
    [string]$AppId = "ee60d27a-fb39-4e43-a2c5-6d04f5e4a5b4",
    [string]$RedirectUri = "http://localhost/signin",
    # Azure CLI 的公共客户端 ID，用来拿 Graph 的委托令牌（不需要装 az）
    [string]$ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$OutputEncoding = [Text.Encoding]::UTF8
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$authority = "https://login.microsoftonline.com/common/oauth2/v2.0"
$graph = "https://graph.microsoft.com/v1.0"
$scope = "https://graph.microsoft.com/Application.ReadWrite.All offline_access"

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# ---------- 1. 设备码 ----------
Write-Step "申请设备码"
$dc = Invoke-RestMethod -Method Post -Uri "$authority/devicecode" -Body @{
    client_id = $ClientId
    scope     = $scope
}
Write-Host ""
Write-Host "  请在浏览器打开： " -NoNewline
Write-Host $dc.verification_uri -ForegroundColor Yellow
Write-Host "  输入代码：       " -NoNewline
Write-Host $dc.user_code -ForegroundColor Green
Write-Host ""
Write-Host "  （用注册 Easyer Launcher 那个微软账号登录，登录后同意权限）"
try { Start-Process $dc.verification_uri } catch { }

# ---------- 2. 轮询拿令牌 ----------
Write-Step "等待你在浏览器完成登录"
$token = $null
$deadline = (Get-Date).AddSeconds([int]$dc.expires_in)
while (-not $token) {
    if ((Get-Date) -gt $deadline) { throw "设备码已过期，请重新运行脚本。" }
    Start-Sleep -Seconds ([int]$dc.interval)
    try {
        $token = Invoke-RestMethod -Method Post -Uri "$authority/token" -Body @{
            client_id  = $ClientId
            grant_type = "urn:ietf:params:oauth:grant-type:device_code"
            device_code = $dc.device_code
        }
    } catch {
        $body = $null
        try { $body = $_.ErrorDetails.Message | ConvertFrom-Json } catch { }
        if ($body -and $body.error -eq "authorization_pending") { continue }
        if ($body -and $body.error -eq "slow_down") { Start-Sleep -Seconds 5; continue }
        if ($body -and $body.error) { throw "登录失败：$($body.error) - $($body.error_description)" }
        throw
    }
}
Write-Host "  登录成功。" -ForegroundColor Green
$h = @{ Authorization = "Bearer $($token.access_token)" }

# ---------- 3. 找 objectId ----------
Write-Step "查找应用 $AppId"
$list = Invoke-RestMethod -Method Get -Headers $h -Uri "$graph/applications?`$filter=appId eq '$AppId'&`$select=id,appId,displayName,signInAudience,publicClient,isFallbackPublicClient"
if (-not $list.value -or $list.value.Count -eq 0) {
    throw "这个账号下找不到 appId=$AppId 的应用。确认用的是注册应用时的账号，或改用 -AppId 指定。"
}
$app = $list.value[0]
Write-Host "  找到：$($app.displayName)  objectId=$($app.id)"
Write-Host "  当前 signInAudience = $($app.signInAudience)"
Write-Host "  当前 isFallbackPublicClient = $($app.isFallbackPublicClient)"

# ---------- 4. PATCH ----------
Write-Step "写入配置（3 项）"
$body = @{
    signInAudience         = "AzureADandPersonalMicrosoftAccount"
    isFallbackPublicClient = $true
    publicClient           = @{ redirectUris = @($RedirectUri) }
    api                    = @{ requestedAccessTokenVersion = 2 }
} | ConvertTo-Json -Depth 5

$patched = Invoke-RestMethod -Method Patch -Headers $h -ContentType "application/json" -Body $body `
    -Uri "$graph/applications/$($app.id)"
Write-Host "  signInAudience  = $($patched.signInAudience)" -ForegroundColor Green
Write-Host "  isFallbackPublicClient = $($patched.isFallbackPublicClient)" -ForegroundColor Green
Write-Host "  publicClient.redirectUris = $($patched.publicClient.redirectUris -join ', ')" -ForegroundColor Green

# ---------- 5. 校验 ----------
Write-Step "重新读取校验"
$check = Invoke-RestMethod -Method Get -Headers $h -Uri "$graph/applications/$($app.id)?`$select=signInAudience,publicClient,isFallbackPublicClient,api"
$ok = ($check.signInAudience -eq "AzureADandPersonalMicrosoftAccount") -and $check.isFallbackPublicClient -and ($check.publicClient.redirectUris -contains $RedirectUri)
if ($ok) {
    Write-Host ""
    Write-Host "  ✓ 全部到位。现在回启动器 → 账号页 → 微软正版登录 → 「浏览器登录（推荐）」。" -ForegroundColor Green
    Write-Host "  应用 ID（Client ID）填：$AppId"
} else {
    Write-Host ""
    Write-Host "  ✗ 还有项没写进去，把上面的输出发我。" -ForegroundColor Red
    $check | ConvertTo-Json -Depth 5
}
