param(
  [Parameter(Mandatory = $true)]
  [string]$WorkspaceFolder
)

$chromeCandidates = @(
  (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'),
  (Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe'),
  (Join-Path $env:LOCALAPPDATA 'Google\Chrome\Application\chrome.exe')
)

$chrome = $chromeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $chrome) {
  $chrome = 'chrome.exe'
}

$extensionSource = (Resolve-Path -LiteralPath (Join-Path $WorkspaceFolder 'artifacts\chromium-extension\source')).Path

Write-Host "Launching Chrome from: $chrome"
Write-Host "Using extension source: $extensionSource"

& npx --yes web-ext run `
  --target chromium `
  --source-dir "$extensionSource" `
  --chromium-binary "$chrome" `
  --start-url chrome://extensions `
  --arg=--no-first-run `
  --arg=--no-default-browser-check `
  --arg=--disable-search-engine-choice-screen
