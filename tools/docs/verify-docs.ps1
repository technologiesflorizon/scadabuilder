param(
    [switch]$StrictCodeDocs
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$docsRoot = Join-Path $repoRoot "docs"
$srcRoot = Join-Path $repoRoot "src"

$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Add-ErrorMessage {
    param([string]$Message)
    $errors.Add($Message) | Out-Null
}

function Add-WarningMessage {
    param([string]$Message)
    $warnings.Add($Message) | Out-Null
}

function Test-MarkdownHeader {
    param([System.IO.FileInfo]$File)

    $lines = Get-Content -LiteralPath $File.FullName
    if ($lines.Count -lt 8) {
        Add-ErrorMessage "$($File.FullName): document is too short for mandatory header."
        return
    }

    if ($lines[0] -notmatch '^# ') {
        Add-ErrorMessage "$($File.FullName): missing H1 on first line."
    }

    if (-not ($lines | Select-String -Pattern '^Date:' -Quiet)) {
        Add-ErrorMessage "$($File.FullName): missing Date metadata."
    }

    if (-not ($lines | Select-String -Pattern '^Status:' -Quiet)) {
        Add-ErrorMessage "$($File.FullName): missing Status metadata."
    }

    if (-not ($lines | Select-String -Pattern '^Document version:\s+`V2\.\d+\.\d+\.\d+`' -Quiet)) {
        Add-ErrorMessage "$($File.FullName): missing or invalid Document version metadata."
    }

    if (-not ($lines | Select-String -Pattern '^## Historique des changements' -Quiet)) {
        Add-ErrorMessage "$($File.FullName): missing change history section."
    }

    if (-not ($lines | Select-String -Pattern '^\| Date \| Version \| Commit \| Changement \|' -Quiet)) {
        Add-ErrorMessage "$($File.FullName): missing mandatory change history table header."
    }
}

function Get-DecisionIds {
    param([string]$Text)

    return [regex]::Matches($Text, 'DEC-\d{4}') |
        ForEach-Object { $_.Value } |
        Where-Object { $_ -ne "DEC-0000" } |
        Sort-Object -Unique
}

function Test-DecisionRegister {
    $registerPath = Join-Path $docsRoot "00_governance\DECISION_REGISTER_V2.md"
    if (-not (Test-Path -LiteralPath $registerPath)) {
        Add-ErrorMessage "Missing decision register: $registerPath"
        return @()
    }

    $text = Get-Content -Raw -LiteralPath $registerPath
    $registered = Get-DecisionIds $text

    foreach ($id in $registered) {
        $sectionPattern = "### $id "
        if ($text -notmatch [regex]::Escape($sectionPattern)) {
            Add-WarningMessage "$id appears outside a level-3 decision heading."
        }
    }

    foreach ($match in [regex]::Matches($text, '### (DEC-\d{4})[^\r\n]*(?<body>.*?)(?=\r?\n### DEC-\d{4}|\z)', 'Singleline')) {
        $id = $match.Groups[1].Value
        $body = $match.Groups["body"].Value
        if ($body -notmatch 'Status:\s+(Active|Deprecated|Superseded)') {
            Add-ErrorMessage "${id}: missing valid Status."
        }
        if ($body -notmatch 'Owner document:\s+`docs/') {
            Add-ErrorMessage "${id}: missing owner document."
        }
        if ($body -match 'Status:\s+(Deprecated|Superseded)' -and $body -notmatch 'Deprecated:\s+\d{4}-\d{2}-\d{2}') {
            Add-ErrorMessage "${id}: deprecated/superseded decision needs deprecated datetime."
        }
    }

    return $registered
}

function Test-DecisionReferences {
    param([string[]]$Registered)

    $allDocsText = Get-ChildItem -LiteralPath $docsRoot -Recurse -File -Filter *.md |
        ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }

    $referenced = $allDocsText | ForEach-Object { Get-DecisionIds $_ } | Sort-Object -Unique
    foreach ($id in $referenced) {
        if ($Registered -notcontains $id) {
            Add-ErrorMessage "Unknown decision reference: $id"
        }
    }
}

function Test-MermaidRequiredDocs {
    $required = @(
        "02_architecture\GLOBAL_ARCHITECTURE_V2.md",
        "02_architecture\APPLICATION_FLOW_V2.md",
        "04_editor\COMMANDS_CONTRACT_V2.md",
        "04_editor\STATE_MANAGEMENT_CONTRACT_V2.md",
        "04_editor\ACTIONS_EVENTS_CONTRACT_V2.md",
        "04_editor\MENUS_AND_SURFACES_CONTRACT_V2.md",
        "03_runtime_contracts\FT100_TF100WEB_PACKAGE_CONTRACT_V2.md",
        "05_studio_element_plus\STUDIO_ELEMENT_PLUS_ARCHITECTURE_V2.md"
    )

    foreach ($relativePath in $required) {
        $path = Join-Path $docsRoot $relativePath
        if (-not (Test-Path -LiteralPath $path)) {
            Add-ErrorMessage "Missing Mermaid owner document: docs/$relativePath"
            continue
        }

        $text = Get-Content -Raw -LiteralPath $path
        if ($text -notmatch '```mermaid') {
            Add-ErrorMessage "Missing Mermaid diagram in docs/$relativePath"
        }
    }
}

function Test-GeneratedDocs {
    $required = @(
        "10_generated\CODE_MAP_V2.md",
        "10_generated\MODULE_FUNCTION_INDEX_V2.md",
        "10_generated\COMMAND_FLOW_DIAGRAM_V2.md",
        "10_generated\STATE_FLOW_DIAGRAM_V2.md",
        "10_generated\EXPORT_FLOW_DIAGRAM_V2.md",
        "10_generated\STUDIO_ELEMENT_PLUS_FLOW_DIAGRAM_V2.md",
        "10_generated\RUNTIME_CAPABILITY_MATRIX_V2.md"
    )

    foreach ($relativePath in $required) {
        $path = Join-Path $docsRoot $relativePath
        if (-not (Test-Path -LiteralPath $path)) {
            Add-ErrorMessage "Missing generated documentation file: docs/$relativePath"
        }
    }

    $matrixGenerator = Join-Path $PSScriptRoot "generate-runtime-capability-matrix.ps1"
    if (-not (Test-Path -LiteralPath $matrixGenerator)) {
        Add-ErrorMessage "Missing runtime capability matrix generator: $matrixGenerator"
        return
    }

    $matrixCheck = & powershell -NoProfile -ExecutionPolicy Bypass -File $matrixGenerator -Check 2>&1
    if ($LASTEXITCODE -ne 0) {
        Add-ErrorMessage "Runtime capability matrix verification failed: $($matrixCheck -join ' ')"
    }
}

function Test-PublicCSharpDocs {
    if (-not (Test-Path -LiteralPath $srcRoot)) {
        Add-WarningMessage "No src directory found for C# documentation scan."
        return
    }

    $publicPattern = '^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+|record\s+|class\s+|interface\s+|enum\s+|struct\s+|readonly\s+|async\s+|virtual\s+|override\s+|required\s+|[\w<>\[\],\?]+\s+)'
    $files = Get-ChildItem -LiteralPath $srcRoot -Recurse -File -Filter *.cs |
        Where-Object {
            $_.FullName -notmatch '\\bin\\' -and
            $_.FullName -notmatch '\\obj\\'
        }

    $missingByFile = @{}

    foreach ($file in $files) {
        $lines = Get-Content -LiteralPath $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($line -match $publicPattern -and $line -notmatch '^\s*public\s+(get|set)\s*;') {
                $previous = if ($i -gt 0) { $lines[$i - 1] } else { "" }
                if ($previous -notmatch '^\s*///') {
                    $relative = Resolve-Path -Relative $file.FullName
                    $message = "${relative}:$($i + 1): public API may be missing XML documentation."
                    if ($StrictCodeDocs) {
                        Add-ErrorMessage $message
                    } else {
                        if (-not $missingByFile.ContainsKey($relative)) {
                            $missingByFile[$relative] = 0
                        }
                        $missingByFile[$relative]++
                    }
                }
            }
        }
    }

    if (-not $StrictCodeDocs) {
        $total = ($missingByFile.Values | Measure-Object -Sum).Sum
        if ($total -gt 0) {
            Add-WarningMessage "Public C# XML documentation debt: $total potential missing docs across $($missingByFile.Keys.Count) files. Run with -StrictCodeDocs for line-level failures."
            foreach ($path in ($missingByFile.Keys | Sort-Object)) {
                Add-WarningMessage "  ${path}: $($missingByFile[$path]) potential missing XML docs."
            }
        }
    }
}

function Test-HighRiskTerms {
    $docsText = Get-ChildItem -LiteralPath $docsRoot -Recurse -File -Filter *.md |
        Where-Object { $_.FullName -notmatch '\\09_archive\\deprecated\\' } |
        ForEach-Object {
            [PSCustomObject]@{
                Path = $_.FullName
                Text = Get-Content -Raw -LiteralPath $_.FullName
            }
        }

    foreach ($doc in $docsText) {
        if ($doc.Text -match 'Open Decisions|Open Questions|Questions ouvertes') {
            Add-WarningMessage "$($doc.Path): legacy open-decision heading remains; prefer DECISION_REGISTER_V2.md."
        }
        if ($doc.Text -match 'PENDING') {
            Add-WarningMessage "$($doc.Path): contains PENDING commit references; replace after commit exists."
        }
    }
}

Get-ChildItem -LiteralPath $docsRoot -Recurse -File -Filter *.md | ForEach-Object {
    Test-MarkdownHeader $_
}

$registered = Test-DecisionRegister
Test-DecisionReferences $registered
Test-MermaidRequiredDocs
Test-GeneratedDocs
Test-PublicCSharpDocs
Test-HighRiskTerms

Write-Host "SCADA Builder V2 documentation verification"
Write-Host "Errors: $($errors.Count)"
foreach ($errorMessage in $errors) {
    Write-Host "[ERROR] $errorMessage"
}

Write-Host "Warnings: $($warnings.Count)"
foreach ($warningMessage in $warnings) {
    Write-Host "[WARN] $warningMessage"
}

if ($errors.Count -gt 0) {
    exit 1
}

exit 0
