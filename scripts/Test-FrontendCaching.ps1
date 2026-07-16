[CmdletBinding()]
param(
    [string]$PublishDirectory,
    [switch]$RequireVersionMetadata
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $repoRoot 'Planora.Web/wwwroot/staticwebapp.config.json'
$config = Get-Content -Raw $configPath | ConvertFrom-Json

if ($config.globalHeaders.'Cache-Control' -notmatch '(^|,)\s*no-cache' -or $config.globalHeaders.'Cache-Control' -notmatch 'must-revalidate') {
    throw "The default cache policy must revalidate mutable and navigation-fallback responses. Actual: $($config.globalHeaders.'Cache-Control')"
}

function Get-RouteCacheControl([string]$Route) {
    $rule = $config.routes | Where-Object { $_.route -eq $Route } | Select-Object -First 1
    if ($null -eq $rule) { throw "Missing cache rule for $Route." }
    return $rule.headers.'Cache-Control'
}

$revalidatedRoutes = @(
    '/index.html', '/version.json', '/service-worker.js', '/service-worker.published.js',
    '/service-worker-assets.js', '/appsettings.json', '/_framework/blazor.boot.json',
    '/_framework/dotnet.js', '/js/cover-cropper.js'
)

foreach ($route in $revalidatedRoutes) {
    $cacheControl = Get-RouteCacheControl $route
    if ($cacheControl -notmatch '(^|,)\s*no-cache' -or $cacheControl -notmatch 'must-revalidate') {
        throw "$route must use a revalidation cache policy. Actual: $cacheControl"
    }
}

foreach ($route in @('/_framework/*', '/css/*', '/lib/*', '/js/*')) {
    $cacheControl = Get-RouteCacheControl $route
    if ($cacheControl -ne 'public, max-age=31536000, immutable') {
        throw "$route must use immutable caching only for fingerprinted assets. Actual: $cacheControl"
    }
}

if (-not $PublishDirectory) {
    Write-Output 'Frontend cache policy source validation passed.'
    return
}

$publishPath = Resolve-Path $PublishDirectory
$indexPath = Join-Path $publishPath 'index.html'
if (-not (Test-Path $indexPath)) { throw "Published index.html was not found at $indexPath." }

$index = Get-Content -Raw $indexPath
if ($index -match '#\[\.\{fingerprint\}\]') {
    throw 'Published index.html still contains unresolved static-asset fingerprint markers.'
}
if ($index -notmatch 'src="js/deployment-version\.[a-z0-9]+\.js"') {
    throw 'Published index.html does not contain a fingerprinted deployment-version script.'
}

foreach ($asset in @('service-worker.js', 'service-worker.published.js')) {
    if (-not (Test-Path (Join-Path $publishPath $asset))) {
        throw "Published output is missing the service-worker retirement entry point: $asset"
    }
}

if ($RequireVersionMetadata) {
    if ($index -match '__PLANORA_BUILD_VERSION__') {
        throw 'Published index.html still contains the build-version placeholder.'
    }
    if ($index -notmatch 'css/app\.css\?v=[0-9a-f]{40}') {
        throw 'Published index.html does not contain a commit-versioned application stylesheet URL.'
    }

    $versionPath = Join-Path $publishPath 'version.json'
    if (-not (Test-Path $versionPath)) { throw 'Published output is missing version.json.' }
    $version = Get-Content -Raw $versionPath | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($version.version) -or [string]::IsNullOrWhiteSpace($version.commit) -or $null -eq $version.builtAtUtc) {
        throw 'version.json must contain version, commit, and builtAtUtc.'
    }
}

Write-Output 'Frontend cache policy and publish-output validation passed.'
