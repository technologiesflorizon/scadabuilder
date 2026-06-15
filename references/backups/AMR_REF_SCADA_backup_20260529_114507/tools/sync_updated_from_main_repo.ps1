param(
  [string]$SourceRoot = "F:\MetroSearch\SCADA_AMR_GROUP\08_web_modernized",
  [string]$ProjectRoot = "F:\MetroSearch\SCADA_AMR_GROUP\SCADA_BUILDER\AMR_SCADA\AMR_REF_SCADA"
)

$ErrorActionPreference = "Stop"

$src = (Resolve-Path $SourceRoot).Path
$dst = Join-Path $ProjectRoot "08_web_modernized"

if (-not (Test-Path $src)) {
  throw "Source not found: $src"
}

New-Item -ItemType Directory -Path $dst -Force | Out-Null

Write-Host "Sync source: $src"
Write-Host "Sync target: $dst"

Copy-Item -Path (Join-Path $src "*") -Destination $dst -Recurse -Force

$updatedPages = (Get-ChildItem -Path (Join-Path $dst "html_pages") -File -Filter "win*_updated.html").Count
$assetCount = (Get-ChildItem -Path (Join-Path $dst "html_pages\assets") -File).Count
$modernizedAssetCount = (Get-ChildItem -Path (Join-Path $dst "html_pages\assets\modernized") -Recurse -File).Count

Write-Host "Done."
Write-Host "Updated pages: $updatedPages"
Write-Host "Assets (root): $assetCount"
Write-Host "Assets (modernized subtree): $modernizedAssetCount"
