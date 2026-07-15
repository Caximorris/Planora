param(
    [string]$SkillsRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$descriptions = @{}
$metadataChars = 0
$requiredSections = @('Inputs', 'Boundaries', 'Workflow', 'Verification', 'Outputs', 'Composition')

$skillDirs = Get-ChildItem -LiteralPath $SkillsRoot -Directory |
    Where-Object { $_.Name -notlike '_*' } |
    Sort-Object Name

foreach ($dir in $skillDirs) {
    $skillFile = Join-Path $dir.FullName 'SKILL.md'
    $agentFile = Join-Path $dir.FullName 'agents\openai.yaml'

    if (-not (Test-Path -LiteralPath $skillFile)) {
        $errors.Add("$($dir.Name): missing SKILL.md")
        continue
    }

    $content = Get-Content -Raw -LiteralPath $skillFile
    if ($content -match '\[TODO|TODO:') { $errors.Add("$($dir.Name): contains TODO text") }

    $frontmatter = [regex]::Match($content, '(?s)\A---\r?\n(.*?)\r?\n---\r?\n')
    if (-not $frontmatter.Success) {
        $errors.Add("$($dir.Name): invalid YAML frontmatter delimiters")
        continue
    }

    $fields = [regex]::Matches($frontmatter.Groups[1].Value, '(?m)^([a-zA-Z0-9_-]+):\s*(.+)$')
    $fieldNames = @($fields | ForEach-Object { $_.Groups[1].Value })
    if (@($fieldNames | Sort-Object) -join ',' -ne 'description,name') {
        $errors.Add("$($dir.Name): frontmatter must contain only name and description")
    }

    $nameMatch = [regex]::Match($frontmatter.Groups[1].Value, '(?m)^name:\s*(.+)$')
    $descriptionMatch = [regex]::Match($frontmatter.Groups[1].Value, '(?m)^description:\s*(.+)$')
    $name = $nameMatch.Groups[1].Value.Trim(' ', '"', "'")
    $description = $descriptionMatch.Groups[1].Value.Trim(' ', '"', "'")

    if ($name -ne $dir.Name) { $errors.Add("$($dir.Name): name '$name' does not match folder") }
    if ($name -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') { $errors.Add("$($dir.Name): invalid skill name") }
    if ($description -notmatch '\bUse\b') { $errors.Add("$($dir.Name): description lacks a Use trigger") }
    if ($description -notmatch '(?i)do not') { $errors.Add("$($dir.Name): description lacks a do-not-use boundary") }

    $metadataChars += $name.Length + $description.Length + 8
    if ($descriptions.ContainsKey($description)) {
        $errors.Add("$($dir.Name): duplicate description with $($descriptions[$description])")
    } else {
        $descriptions[$description] = $dir.Name
    }

    foreach ($section in $requiredSections) {
        if ($content -notmatch "(?m)^## $([regex]::Escape($section))\s*$") {
            $errors.Add("$($dir.Name): missing ## $section")
        }
    }

    $lineCount = ($content -split "`r?`n").Count
    if ($lineCount -gt 500) { $errors.Add("$($dir.Name): $lineCount lines exceeds 500") }

    if (-not (Test-Path -LiteralPath $agentFile)) {
        $errors.Add("$($dir.Name): missing agents/openai.yaml")
    } else {
        $agentContent = Get-Content -Raw -LiteralPath $agentFile
        if ($agentContent -notmatch [regex]::Escape('$' + $name)) {
            $errors.Add("$($dir.Name): default_prompt does not mention `$$name")
        }
        $short = [regex]::Match($agentContent, '(?m)^\s*short_description:\s*"([^"]+)"')
        if (-not $short.Success -or $short.Groups[1].Value.Length -lt 25 -or $short.Groups[1].Value.Length -gt 64) {
            $errors.Add("$($dir.Name): short_description must be 25-64 characters")
        }
    }
}

if ($metadataChars -gt 8000) {
    $errors.Add("Discovery metadata estimate is $metadataChars characters; keep it at or below 8000")
} elseif ($metadataChars -gt 7200) {
    $warnings.Add("Discovery metadata estimate is $metadataChars characters; descriptions have little growth budget")
}

Write-Output "Validated $($skillDirs.Count) skills; discovery metadata estimate: $metadataChars characters."
foreach ($warning in $warnings) { Write-Warning $warning }
if ($errors.Count -gt 0) {
    foreach ($error in $errors) { Write-Error $error }
    exit 1
}
Write-Output 'Skill library validation passed.'
