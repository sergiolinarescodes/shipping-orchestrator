#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Boots the local shipping-orchestrator dev stack: Postgres + LocalStack + Jaeger + Mailpit
  via docker compose, waits for health, then opens one Windows Terminal window with a tab
  per host (compose logs / public api / private api / worker) and -- when Node tooling is
  available -- two more tabs for the customer & internal dashboards.

.NOTES
  All tabs are opened by a SINGLE `wt` invocation that uses `;` separators between sub-
  commands. Passing `;` as its own argv element from PowerShell's call operator avoids
  both (a) the backtick-line-continuation chain (fragile) and (b) the race condition
  observed when spawning multiple `wt --window 0` processes back-to-back.
#>

param(
    [switch]$Clean,
    # Skip the upfront `dotnet build`. Tabs will then build themselves which can race
    # on the shared Infrastructure.dll (CS2012 file-lock). Only useful if you've
    # already built in your IDE and just want fast tab launch.
    [switch]$SkipBuild,
    # Showcase mode: also start the Shopify Remix app via `shopify app dev` (must be
    # installed via `npm install -g @shopify/cli @shopify/app`) and run PublicApi
    # under the Showcase launch profile so Connectors:Shopify:Mode flips to Real.
    # On by default — pass `-ShowcaseShopify:$false` (or `-NoShowcase`) to opt out.
    [bool]$ShowcaseShopify = $true,
    # Showcase mode (WooCommerce): boots the bundled WordPress + WooCommerce stack at
    # http://localhost:8080 (woocommerce/docker-compose.yml) and flips
    # Connectors:WooCommerce:Mode to Real via the Showcase launch profile.
    # On by default — pass `-ShowcaseWooCommerce:$false` (or `-NoShowcase`) to opt out.
    [bool]$ShowcaseWooCommerce = $true,
    # Umbrella escape hatch — disables every connector showcase in one go. Useful for
    # `dotnet test`-style runs where you only want bare infra (Postgres/LocalStack/etc)
    # plus the three .NET hosts and the SPAs, no external integrations.
    [switch]$NoShowcase
)

if ($NoShowcase) {
    $ShowcaseShopify     = $false
    $ShowcaseWooCommerce = $false
}

# Use 'Continue' (not 'Stop'). PowerShell 5.1 turns *any* native-command stderr
# line into a terminating NativeCommandError when ErrorActionPreference='Stop',
# and `docker compose` writes its progress lines to stderr. We check
# $LASTEXITCODE explicitly after each native call instead.
$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Invoke-Native {
    param([Parameter(Mandatory)] [scriptblock] $Block, [string] $Label = 'native command')
    # Merge stderr into stdout and project ErrorRecord -> string so PowerShell 5.1
    # doesn't paint informational stderr lines (e.g. docker compose progress) red.
    & $Block 2>&1 | ForEach-Object { Write-Host $_.ToString() }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "$Label exited $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "Docker not found. Install Docker Desktop and ensure it is running."
    exit 1
}

if ($Clean) {
    Write-Host "Removing previous containers + volumes..." -ForegroundColor Yellow
    Invoke-Native -Label 'docker compose down' -Block { docker compose down -v }
}

Write-Host "Starting infra..." -ForegroundColor Cyan
Invoke-Native -Label 'docker compose up' -Block { docker compose up -d }

function Wait-Healthy {
    param([Parameter(Mandatory)] [string] $Container, [int] $MaxIterations = 60)
    Write-Host "Waiting for $Container healthcheck..." -ForegroundColor Cyan
    for ($i = 0; $i -lt $MaxIterations; $i++) {
        $health = (docker inspect --format '{{.State.Health.Status}}' $Container 2>&1 | Out-String).Trim()
        if ($health -eq 'healthy') { return }
        Start-Sleep -Seconds 2
    }
    Write-Host "$Container not healthy after $($MaxIterations * 2)s" -ForegroundColor Yellow
}

Wait-Healthy 'shipping-orchestrator-postgres'
Wait-Healthy 'shipping-orchestrator-localstack'

Write-Host ""
Write-Host "Stack is up:" -ForegroundColor Green
Write-Host "  Postgres : localhost:5432  (db=shipping_orchestrator user=app)" -ForegroundColor Gray
Write-Host "  LocalStack: http://localhost:4566" -ForegroundColor Gray
Write-Host "  Jaeger    : http://localhost:16686" -ForegroundColor Gray
Write-Host "  Mailpit   : http://localhost:8025" -ForegroundColor Gray
Write-Host ""

# ---- pre-build the solution to avoid CS2012 file-lock races ---------------
# When PublicApi/PrivateApi/Worker each invoke `dotnet build` in their own tab
# concurrently, MSBuild can't take exclusive write locks on the shared
# Infrastructure.dll output and one (or more) builds fail with CS2012. Building
# once up front, then starting each tab with --no-build, sidesteps this entirely.
$dotnetRunFlags = '--no-build'
if (-not $SkipBuild) {
    Write-Host "Pre-building solution..." -ForegroundColor Cyan
    Invoke-Native -Label 'dotnet build' -Block { dotnet build ShippingOrchestrator.slnx --nologo -v m }
} else {
    Write-Host "Skipping pre-build (-SkipBuild). Tabs will build on launch -- CS2012 races possible." -ForegroundColor Yellow
    $dotnetRunFlags = ''
}

# ---- dashboard launcher detection -----------------------------------------
$webRoot = Join-Path $repoRoot 'web'
$hasWeb  = Test-Path (Join-Path $webRoot 'package.json')
$pnpmExe   = $null  # bare exe name passed to wt (no args)
$pnpmExtra = @()    # extra args (e.g. for `npx pnpm@x dev:customer`)
if ($hasWeb) {
    if (Get-Command pnpm -ErrorAction SilentlyContinue) {
        $pnpmExe = 'pnpm'
    } elseif (Get-Command npx -ErrorAction SilentlyContinue) {
        # Fall back to a one-off pnpm via npx so the user doesn't need a global install.
        $pnpmExe   = 'npx'
        $pnpmExtra = @('--yes', 'pnpm@9.12.3')
        Write-Host "pnpm not on PATH -- dashboards will run via 'npx pnpm@9.12.3' (slower first launch)." -ForegroundColor Yellow
    } else {
        Write-Host "Neither pnpm nor npx on PATH -- skipping dashboard tabs. Install Node 20+ to enable them." -ForegroundColor Yellow
    }
}

# ---- tab plan -------------------------------------------------------------
function Format-Run([string]$proj, [string]$profile = '') {
    $base = if ($dotnetRunFlags) { "dotnet run $dotnetRunFlags --project $proj" } else { "dotnet run --project $proj" }
    if ($profile) { "$base --launch-profile $profile" } else { $base }
}
$publicApiProfile = if ($ShowcaseShopify -or $ShowcaseWooCommerce) { 'Showcase' } else { '' }

# ---- public webhook tunnel for Real-mode Shopify --------------------------
# Shopify dispatches webhooks from the cloud and can't reach localhost. The Real Shopify
# connector calls the Admin API at install time to register store-specific webhooks against
# whatever public URL we hand it (`Connectors:Shopify:OrchestratorWebhookBaseUrl`). In dev
# we boot a quick cloudflared tunnel here, parse its assigned hostname out of the log, and
# inject it via env var into the public-api WT tab so the value never has to land in a
# committed config file. Run-to-run the URL changes; that's fine because the env var is
# scoped to the tab's process.
$shopifyWebhookBaseUrl = $null
$tunnelLog = Join-Path $env:TEMP 'shipping-orchestrator-tunnel.log'

# Resolve the cloudflared executable. winget's silent install of Cloudflare.cloudflared
# doesn't always create a shim on PATH, so fall back to scanning the known WinGet package
# directory. If neither hits, we degrade to "no tunnel" with a clear instruction.
function Resolve-Cloudflared {
    $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $wingetCandidate = Get-ChildItem -Path "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" `
        -Filter 'cloudflared.exe' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($wingetCandidate) { return $wingetCandidate.FullName }
    return $null
}

if ($ShowcaseShopify) {
    $cloudflaredExe = Resolve-Cloudflared
    if (-not $cloudflaredExe) {
        Write-Host "-ShowcaseShopify needs a public webhook URL, but 'cloudflared' was not found." -ForegroundColor Yellow
        Write-Host "  Install:  winget install --id Cloudflare.cloudflared" -ForegroundColor Yellow
        Write-Host "  Continuing without a tunnel - Shopify webhook auto-registration will log a warning and skip." -ForegroundColor Yellow
    } else {
        Remove-Item $tunnelLog -Force -ErrorAction SilentlyContinue
        Start-Process -FilePath $cloudflaredExe `
            -ArgumentList @('tunnel', '--url', 'http://localhost:5101', '--no-autoupdate') `
            -RedirectStandardOutput $tunnelLog `
            -RedirectStandardError "$tunnelLog.err" `
            -WindowStyle Hidden | Out-Null
        Write-Host "Starting cloudflared tunnel for Shopify webhooks..." -ForegroundColor Cyan
        $deadline = (Get-Date).AddSeconds(25)
        while ((Get-Date) -lt $deadline -and -not $shopifyWebhookBaseUrl) {
            Start-Sleep -Milliseconds 500
            $sources = @($tunnelLog, "$tunnelLog.err") | Where-Object { Test-Path $_ }
            foreach ($src in $sources) {
                $hit = Select-String -Path $src -Pattern 'https://[a-z-]+\.trycloudflare\.com' -List -ErrorAction SilentlyContinue
                if ($hit) {
                    $shopifyWebhookBaseUrl = $hit.Matches[0].Value
                    break
                }
            }
        }
        if ($shopifyWebhookBaseUrl) {
            Write-Host "  Tunnel    : $shopifyWebhookBaseUrl  -> http://localhost:5101" -ForegroundColor Gray
        } else {
            Write-Host "Could not detect tunnel URL within 25s. cloudflared log: $tunnelLog" -ForegroundColor Red
        }
    }
}

# Inject the resolved tunnel URL into the public-api tab as a session env var so neither
# launchSettings.json nor any other committed file ever needs to carry an ephemeral URL.
$publicApiCmd = Format-Run 'src/ShippingOrchestrator.PublicApi' $publicApiProfile
if ($shopifyWebhookBaseUrl) {
    $publicApiCmd = "`$env:Connectors__Shopify__OrchestratorWebhookBaseUrl='$shopifyWebhookBaseUrl'; $publicApiCmd"
}

$plan = @(
    @{ Title = 'compose';     Cwd = $repoRoot; Cmd = 'docker compose logs -f' },
    @{ Title = 'public-api';  Cwd = $repoRoot; Cmd = $publicApiCmd },
    @{ Title = 'private-api'; Cwd = $repoRoot; Cmd = (Format-Run 'src/ShippingOrchestrator.PrivateApi') },
    @{ Title = 'worker';      Cwd = $repoRoot; Cmd = (Format-Run 'src/ShippingOrchestrator.Worker') }
)
if ($shopifyWebhookBaseUrl) {
    # Visible tunnel tab so the user can see incoming webhook requests + the tunnel URL.
    $plan += @{ Title = 'tunnel'; Cwd = $repoRoot; Cmd = "Get-Content -Path '$tunnelLog' -Wait" }
}

if ($pnpmExe) {
    $cust = if ($pnpmExtra.Count -gt 0) { "$pnpmExe $($pnpmExtra -join ' ') dev:customer" } else { "$pnpmExe dev:customer" }
    $intr = if ($pnpmExtra.Count -gt 0) { "$pnpmExe $($pnpmExtra -join ' ') dev:internal" } else { "$pnpmExe dev:internal" }
    $plan += @{ Title = 'ui-customer'; Cwd = $webRoot; Cmd = $cust }
    $plan += @{ Title = 'ui-internal'; Cwd = $webRoot; Cmd = $intr }
}

if ($ShowcaseShopify) {
    $shopifyAppRoot = Join-Path $repoRoot 'shopify-app'
    if (-not (Test-Path (Join-Path $shopifyAppRoot 'package.json'))) {
        Write-Host "-ShowcaseShopify set but $shopifyAppRoot is not scaffolded yet." -ForegroundColor Yellow
        Write-Host "  Run:  cd shopify-app; shopify app config link  (see SHOWCASE.md)" -ForegroundColor Yellow
    } elseif (-not (Get-Command shopify -ErrorAction SilentlyContinue)) {
        Write-Host "-ShowcaseShopify set but 'shopify' CLI not on PATH." -ForegroundColor Yellow
        Write-Host "  Install:  npm install -g @shopify/cli @shopify/app" -ForegroundColor Yellow
    } else {
        $plan += @{ Title = 'shopify-app'; Cwd = $shopifyAppRoot; Cmd = 'shopify app dev' }
    }
}

if ($ShowcaseWooCommerce) {
    $wcRoot = Join-Path $repoRoot 'woocommerce'
    if (-not (Test-Path (Join-Path $wcRoot 'docker-compose.yml'))) {
        Write-Host "-ShowcaseWooCommerce set but $wcRoot/docker-compose.yml not found." -ForegroundColor Yellow
    } else {
        Write-Host "Starting WooCommerce stack (WP + WC + MariaDB on :8080)..." -ForegroundColor Cyan
        Push-Location $wcRoot
        Invoke-Native -Label 'wc docker compose up' -Block { docker compose up -d }
        Pop-Location
        $plan += @{ Title = 'woocommerce'; Cwd = $wcRoot; Cmd = 'docker compose logs -f' }
        Write-Host "  WordPress  : http://localhost:8080  (admin / admin once seeded)" -ForegroundColor Gray
    }
}

if (-not (Get-Command wt -ErrorAction SilentlyContinue)) {
    Write-Host "Windows Terminal (wt) not found. Run each host manually:" -ForegroundColor Yellow
    foreach ($t in $plan) {
        Write-Host ("  ({0,-12}) cd {1}; {2}" -f $t.Title, $t.Cwd, $t.Cmd)
    }
    return
}

# ---- single wt invocation -------------------------------------------------
# Build one argv: --window 0 new-tab ...tab1... ; new-tab ...tab2... ; ...
# A bare `;` element in argv (NOT escaped, NOT quoted) is interpreted by wt
# as a sub-command separator.
#
# CRITICAL: any `;` *inside* a tab's command (e.g. the public-api tab's
# `$env:X='...'; dotnet run ...`) leaks through PowerShell 5.1's native-argv
# quoting and wt splits the tab's command at that `;` too — running the env
# var assignment in tab N and trying to launch `" dotnet run ..."` as an
# executable for tab N+1 (error 0x80070002, "system cannot find the file").
#
# We sidestep argv escaping entirely by writing each tab's command to its
# own .ps1 in TEMP and launching it with `powershell -File <path>`. The file
# contents can contain any number of `;` without wt seeing them.
$tabScriptDir = Join-Path $env:TEMP 'shipping-orchestrator-tabs'
if (Test-Path $tabScriptDir) { Remove-Item $tabScriptDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Path $tabScriptDir -Force | Out-Null

$wtArgs = @('--window', '0')
for ($i = 0; $i -lt $plan.Count; $i++) {
    if ($i -gt 0) { $wtArgs += ';' }
    $t = $plan[$i]
    $scriptPath = Join-Path $tabScriptDir ("tab-{0:00}-{1}.ps1" -f $i, $t.Title)
    Set-Content -Path $scriptPath -Value $t.Cmd -Encoding utf8
    $wtArgs += @(
        'new-tab', '--title', $t.Title,
        '-d', $t.Cwd,
        'powershell', '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath
    )
}

Write-Host "Opening Windows Terminal with $($plan.Count) tabs..." -ForegroundColor Cyan
& wt @wtArgs
