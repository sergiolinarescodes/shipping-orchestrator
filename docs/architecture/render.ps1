param(
    [ValidateSet('png','svg','pdf')]
    [string]$Format = 'png',
    [string]$OutDir = "$PSScriptRoot/out"
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command mmdc -ErrorAction SilentlyContinue)) {
    Write-Error "mmdc not found. Install: npm install -g @mermaid-js/mermaid-cli"
    exit 1
}

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

Get-ChildItem -Path $PSScriptRoot -Filter '*.mmd' | ForEach-Object {
    $out = Join-Path $OutDir ($_.BaseName + ".$Format")
    Write-Host "rendering $($_.Name) -> $out"
    mmdc -i $_.FullName -o $out -b white
}

Write-Host "done. files in $OutDir"
