#Requires -Version 5.1
<#
.SYNOPSIS
  Enregistre un checkpoint vert pour la politique Phase Completion / Rollback
  docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md:66

.DESCRIPTION
  - Capture git rev-parse HEAD + branch des deux dépôts
  - Ecrit le fichier versionne tools/quick-window/checkpoints.json (artifacts/ est ignore par git et ne peut pas servir de preuve de rollback)
  - N'utilise jamais git reset --hard; le rollback attendu est git revert en ordre inverse
#>
param(
  [Parameter(Mandatory=$true)][string]$Phase,
  [string]$Notes = ""
)

$ErrorActionPreference = "Stop"

$builderRoot = "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
$tf100Root   = "F:\Projet\Git\TF100Web"
$checkpointFile = Join-Path $builderRoot "tools/quick-window/checkpoints.json"

# Branches attendues. Un checkpoint est une preuve de rollback: enregistrer le HEAD d'une autre
# branche est pire que ne rien enregistrer, parce que rien ne le signale a la lecture.
$expectedBuilderBranch = "codex/GestionFenetreRapide"
$expectedTf100Branch   = "codex/quick-window-v1"

<#
.SYNOPSIS
  Resout le repertoire ou la branche attendue est effectivement checkoutee.
.DESCRIPTION
  Le checkout principal peut etre sur un tout autre chantier - il l'etait le 2026-09-02, sur
  chantier/lorawan-wm1302 - pendant que le travail vit dans un worktree. `git worktree list`
  donne la reponse; sans correspondance la fonction retourne $null et l'appelant echoue.
#>
function Resolve-WorktreeForBranch($root, $branch) {
  $current = (& git -C $root branch --show-current 2>$null)
  if ($current -and $current.Trim() -eq $branch) { return $root }

  $lines = & git -C $root worktree list --porcelain 2>$null
  $path = $null
  foreach ($line in $lines) {
    if ($line -like "worktree *") { $path = $line.Substring(9).Trim() }
    elseif ($line -like "branch *") {
      $found = $line.Substring(7).Trim() -replace "^refs/heads/", ""
      if ($found -eq $branch -and $path) { return $path }
    }
  }
  return $null
}

function Get-GitInfo($root) {
  $head = & git -C $root rev-parse HEAD 2>$null
  $branch = & git -C $root branch --show-current 2>$null
  $status = & git -C $root status --short --branch 2>$null | Out-String
  return @{ head = $head.Trim(); branch = $branch.Trim(); status = $status.Trim() }
}

$builderRoot = Resolve-WorktreeForBranch $builderRoot $expectedBuilderBranch
if (-not $builderRoot) {
  throw "Aucun checkout ni worktree sur '$expectedBuilderBranch' cote Builder: checkpoint refuse."
}
$tf100Resolved = Resolve-WorktreeForBranch $tf100Root $expectedTf100Branch
if (-not $tf100Resolved) {
  throw "Aucun checkout ni worktree sur '$expectedTf100Branch' cote TF100Web: checkpoint refuse."
}
$tf100Root = $tf100Resolved

$builder = Get-GitInfo $builderRoot
$tf100   = Get-GitInfo $tf100Root

# Un worktree sale rend le HEAD non reproductible, donc inutilisable comme point de reprise.
if ($builder.status -notmatch "^## [^
]+$") {
  throw "Worktree Builder non propre; un checkpoint doit pointer un etat reproductible."
}
if ($tf100.status -notmatch "^## [^
]+$") {
  throw "Worktree TF100Web non propre; un checkpoint doit pointer un etat reproductible."
}

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
Write-Host "  TF100Web: $($tf100.branch) $($tf100.head)  ($tf100Root)"
