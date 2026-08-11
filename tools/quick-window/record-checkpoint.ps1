#Requires -Version 5.1
<#
.SYNOPSIS
  Enregistre un checkpoint vert pour la politique Phase Completion / Rollback
  docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md:66

.DESCRIPTION
  - Capture git rev-parse HEAD + branch des deux dépôts
  - Conserve le fichier checkpoints.json sous artifacts/quick-window-rollout/ (non versionné, voir .gitignore: artifacts/)
  - N'utilise jamais git reset --hard; le rollback attendu est git revert en ordre inverse
#>
param(
  [Parameter(Mandatory=$true)][string]$Phase,
  [string]$Notes = ""
)

$ErrorActionPreference = "Stop"

$builderRoot = "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
$tf100Root   = "F:\Projet\Git\TF100Web"
$checkpointFile = Join-Path $builderRoot "artifacts/quick-window-rollout/checkpoints.json"

function Get-GitInfo($root) {
  $head = & git -C $root rev-parse HEAD 2>$null
  $branch = & git -C $root branch --show-current 2>$null
  $status = & git -C $root status --short --branch 2>$null | Out-String
  return @{ head = $head.Trim(); branch = $branch.Trim(); status = $status.Trim() }
}

$builder = Get-GitInfo $builderRoot
$tf100   = Get-GitInfo $tf100Root

$entry = [ordered]@{
  phase = $Phase
  timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
  builder = $builder
  tf100web = $tf100
  dotnetSdk = (& dotnet --version 2>$null).Trim()
  nodeInstalled = (& node --version 2>$null).Trim()
  nvmrc = if (Test-Path (Join-Path $builderRoot ".nvmrc")) { (Get-Content (Join-Path $builderRoot ".nvmrc") -Raw).Trim() } else { $null }
  enginesNode = $null
  notes = $Notes
}

$pkg = Join-Path $builderRoot "tests/runtime-js/package.json"
if (Test-Path $pkg) {
  $j = Get-Content $pkg -Raw | ConvertFrom-Json
  $entry.enginesNode = $j.engines.node
}

$data = $null
if (Test-Path $checkpointFile) {
  $raw = Get-Content $checkpointFile -Raw
  $data = $raw | ConvertFrom-Json
  # Convert PSCustomObject checkpoints to list for append
  $list = @()
  foreach ($c in $data.checkpoints) { $list += $c }
  $list += [pscustomobject]$entry
  $data.checkpoints = $list
} else {
  $data = [ordered]@{
    schemaVersion = "1.0"
    policy = "Phase Completion, Rollback and Version Policy docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md:66"
    versioning = "iteration bumps until Phase 6 feature bump; no production bump without explicit approval"
    rollback = "git revert never reset --hard reverse order"
    checkpoints = @([pscustomobject]$entry)
  }
}

$dir = Split-Path $checkpointFile -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$data | ConvertTo-Json -Depth 6 | Set-Content -Path $checkpointFile -Encoding utf8
Write-Host "Checkpoint $Phase enregistre dans $checkpointFile" -ForegroundColor Green
Write-Host "  Builder: $($builder.branch) $($builder.head)" 
Write-Host "  TF100Web: $($tf100.branch) $($tf100.head)"
