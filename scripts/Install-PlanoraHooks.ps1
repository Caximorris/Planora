[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel 2>$null)
if (-not $root) {
    throw 'Run this installer inside the Planora Git repository.'
}

git -C $root config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to configure core.hooksPath.'
}

Write-Host 'Planora Git hooks enabled through core.hooksPath=.githooks.'
Write-Host 'Codex project hooks must also be reviewed and trusted with /hooks.'
