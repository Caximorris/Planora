[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [uri]$SiteUrl
)

$ErrorActionPreference = 'Stop'
$baseUri = $SiteUrl.AbsoluteUri.TrimEnd('/')

function Get-Response([string]$Path) {
    $relativePath = $Path.TrimStart('/')
    Invoke-WebRequest -Uri "$baseUri/$relativePath" -Method Head -MaximumRedirection 0 -SkipHttpErrorCheck
}

function Assert-Revalidated([string]$Path) {
    $response = Get-Response $Path
    $cacheControl = $response.Headers['Cache-Control']
    if ($cacheControl -notmatch 'no-cache' -or $cacheControl -notmatch 'must-revalidate') {
        throw "$Path must revalidate. Actual Cache-Control: $cacheControl"
    }
}

foreach ($path in @('/', '/index.html', '/version.json', '/service-worker.js', '/service-worker.published.js', '/js/cover-cropper.js')) {
    Assert-Revalidated $path
}

$index = Invoke-WebRequest -Uri "$baseUri/index.html" -SkipHttpErrorCheck
$frameworkAsset = [regex]::Match($index.Content, 'src="(?<path>_framework/blazor\.webassembly[^\"]+\.js)"').Groups['path'].Value
$cssAsset = [regex]::Match($index.Content, 'href="(?<path>css/app\.css\?v=[0-9a-f]{40})"').Groups['path'].Value

foreach ($asset in @($frameworkAsset, $cssAsset)) {
    if ([string]::IsNullOrWhiteSpace($asset)) { throw 'Could not find a fingerprinted deployment asset in index.html.' }
    $cacheControl = (Get-Response $asset).Headers['Cache-Control']
    if ($cacheControl -notmatch 'max-age=31536000' -or $cacheControl -notmatch 'immutable') {
        throw "$asset must be immutable. Actual Cache-Control: $cacheControl"
    }
}

Write-Output "Frontend cache-header smoke test passed for $baseUri."
