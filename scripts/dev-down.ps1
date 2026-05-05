#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Stops the local dev stack. Pass -Clean to also drop volumes. ShowcaseWooCommerce is
  on by default and tears down the bundled WordPress + WooCommerce stack under
  woocommerce/. Pass -ShowcaseWooCommerce:$false (or -NoShowcase) to skip it.
#>
param(
    [switch]$Clean,
    [bool]$ShowcaseWooCommerce = $true,
    [switch]$NoShowcase
)

if ($NoShowcase) { $ShowcaseWooCommerce = $false }

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# Stop any cloudflared tunnel spawned by dev-up. Best-effort: not all environments have
# a cloudflared install, and the user may also have a long-lived named tunnel running
# they don't want killed — but the quick-tunnel one we spawn is a windowless background
# process started by dev-up.ps1 with --url http://localhost:5101, so we match on cmdline.
Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match 'http://localhost:5101' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

if ($Clean) {
    docker compose down -v
} else {
    docker compose down
}

if ($ShowcaseWooCommerce) {
    $wcRoot = Join-Path $repoRoot 'woocommerce'
    if (Test-Path (Join-Path $wcRoot 'docker-compose.yml')) {
        Push-Location $wcRoot
        if ($Clean) { docker compose down -v } else { docker compose down }
        Pop-Location
    }
}
