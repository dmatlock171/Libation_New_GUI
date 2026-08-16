# Full clean rebuild of the Chardonnay dev build.
#
# Why this exists: Avalonia compiles XAML into the assembly at build time, so a stale
# obj directory can leave yesterday's markup in today's binary. An incremental
# "dotnet run" usually notices, but when the UI does not match the source it is
# quicker to rule this out than to argue with it.
#
# Usage:
#   .\clean-build.ps1              # clean, build, report
#   .\clean-build.ps1 -Run         # ...then launch via run-dev.ps1
#   .\clean-build.ps1 -Run -FilesDir "E:\_Libations"

param(
	[switch]$Run,
	[string]$FilesDir = "E:\_LibationClean"
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$sln = Join-Path $root "Source\Libation.slnx"

Write-Host "Repo   : $root" -ForegroundColor Cyan
Write-Host "Commit : $(git -C $root rev-parse --short HEAD) $(git -C $root log -1 --format=%s)" -ForegroundColor Cyan

# Warn about uncommitted or unbuilt state before spending time on a build
$dirty = git -C $root status --porcelain | Where-Object { $_ -notmatch '^\?\?' }
if ($dirty) {
	Write-Host "Note: working tree has uncommitted changes." -ForegroundColor Yellow
}

Write-Host "`nRemoving bin and obj..." -ForegroundColor Cyan
$dirs = Get-ChildItem -Path (Join-Path $root "Source") -Recurse -Directory -Force |
	Where-Object { $_.Name -in 'bin', 'obj' } |
	Sort-Object { $_.FullName.Length } -Descending

foreach ($d in $dirs) {
	if (Test-Path $d.FullName) {
		Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction SilentlyContinue
	}
}
Write-Host "  removed $($dirs.Count) directories"

Write-Host "`nBuilding..." -ForegroundColor Cyan
dotnet build $sln --nologo -v minimal

if ($LASTEXITCODE -ne 0) {
	Write-Error "Build failed."
	exit $LASTEXITCODE
}

Write-Host "`nBuild succeeded." -ForegroundColor Green

if ($Run) {
	& (Join-Path $root "run-dev.ps1") -FilesDir $FilesDir
}
else {
	Write-Host "Run it with:  .\run-dev.ps1" -ForegroundColor Cyan
}
