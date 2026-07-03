# Install / link 4 token-saving AI tools for StockRadar
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-ai-tools.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$uaRepo = Join-Path $env:USERPROFILE ".understand-anything\repo"
$cursor = Join-Path $env:LOCALAPPDATA "Programs\cursor\Cursor.exe"
$extDir = Join-Path $env:USERPROFILE ".cursor\extensions"

Write-Host "=== StockRadar AI tools setup ===" -ForegroundColor Cyan

# 1) Continue extension (win32-x64 — NOT universal VSIX)
Write-Host "`n[1/4] Continue.dev" -ForegroundColor Yellow
$continueExt = Join-Path $extDir "Continue.continue-2.1.0-win32-x64"
$continueBin = Join-Path $continueExt "bin\napi-v3\win32\x64\onnxruntime_binding.node"
if ((Test-Path $continueExt) -and (Test-Path $continueBin)) {
    Write-Host "  OK: Continue win32-x64 at $continueExt"
} else {
    Write-Host "  Fixing Continue (wrong build or missing Windows binary)..."
    & (Join-Path $PSScriptRoot "fix-continue-extension.ps1")
}

# 2) Cursor rules + CLAUDE.md (in repo)
Write-Host "`n[2/4] .cursor/rules + CLAUDE.md" -ForegroundColor Yellow
@(
    (Join-Path $root ".cursor\rules\token-cost-efficiency.mdc"),
    (Join-Path $root "CLAUDE.md")
) | ForEach-Object {
    if (Test-Path $_) { Write-Host "  OK: $_" } else { Write-Host "  Missing: $_" -ForegroundColor Red }
}

# 3) Understand-Anything - clone + junction for Cursor auto-discovery
Write-Host "`n[3/4] Understand-Anything" -ForegroundColor Yellow
if (-not (Test-Path (Join-Path $uaRepo ".git"))) {
    git clone --depth 1 https://github.com/Egonex-AI/Understand-Anything.git $uaRepo
} else {
    git -C $uaRepo pull --ff-only 2>$null
}

function Link-Junction($link, $target) {
    if (Test-Path $link) {
        $item = Get-Item -LiteralPath $link -Force
        if ($item.LinkType -eq "Junction") { return }
        Write-Host "  Skip $link (exists, not a junction)" -ForegroundColor DarkYellow
        return
    }
    New-Item -ItemType Junction -Path $link -Target $target | Out-Null
    Write-Host "  Junction: $link -> $target"
}

Link-Junction (Join-Path $root ".cursor-plugin") (Join-Path $uaRepo ".cursor-plugin")
Link-Junction (Join-Path $root "understand-anything-plugin") (Join-Path $uaRepo "understand-anything-plugin")

Write-Host "  After reload Cursor, try: /understand"
Write-Host "  If missing: Settings -> Plugins -> add https://github.com/Egonex-AI/Understand-Anything"

# 4) Repomix CLI
Write-Host "`n[4/4] Repomix" -ForegroundColor Yellow
if (-not (Get-Command repomix -ErrorAction SilentlyContinue)) {
    npm install -g repomix@latest
}
$ver = repomix --version 2>$null
Write-Host "  OK: repomix $ver"
Write-Host "  Pack on demand: scripts\repomix-pack.ps1"

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "Reload Cursor, open Continue sidebar, run /understand once (uses tokens on first run)."
