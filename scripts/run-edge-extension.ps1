param(
  [Parameter(Mandatory = $true)]
  [string]$WorkspaceFolder
)

$edgeCandidates = @(
  (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe'),
  (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'),
  (Join-Path $env:LOCALAPPDATA 'Microsoft\Edge\Application\msedge.exe')
)

$edge = $edgeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $edge) {
  $edge = 'msedge.exe'
}

$extensionSource = (Resolve-Path -LiteralPath (Join-Path $WorkspaceFolder 'artifacts\edge-extension\source')).Path

Write-Host "Launching Edge from: $edge"
Write-Host "Using extension source: $extensionSource"

& npx --yes web-ext run `
  --target chromium `
  --source-dir "$extensionSource" `
  --chromium-binary "$edge" `
  --start-url edge://extensions `
  --arg=--no-first-run `
  --arg=--no-default-browser-check
