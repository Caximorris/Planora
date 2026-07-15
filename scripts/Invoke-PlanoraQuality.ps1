[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('commit', 'pre-migration', 'pr', 'completion')]
    [string]$Gate,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel 2>$null)
if (-not $root) {
    throw 'Planora quality gates must run inside the Git repository.'
}

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    $python = Get-Command python3 -ErrorAction SilentlyContinue
}
if (-not $python) {
    $python = Get-Command py -ErrorAction SilentlyContinue
}
if (-not $python) {
    throw 'Python 3 is required for the Planora quality orchestrator.'
}

$arguments = @(
    (Join-Path $root '.codex/hooks/planora_hooks.py'),
    'run',
    '--gate',
    $Gate
)
if ($Force) {
    $arguments += '--force'
}

& $python.Source @arguments
exit $LASTEXITCODE
