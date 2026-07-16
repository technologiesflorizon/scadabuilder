param(
    [switch]$Check
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$generatorProject = Join-Path $repoRoot "tools\RuntimeCapabilityMatrixGenerator\RuntimeCapabilityMatrixGenerator.csproj"
$arguments = @(
    "run",
    "--project", $generatorProject,
    "--verbosity", "quiet",
    "--",
    "--repo-root", $repoRoot
)

if ($Check) {
    $arguments += "--check"
}

& dotnet @arguments
exit $LASTEXITCODE
