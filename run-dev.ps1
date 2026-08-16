# Launches the development build of Libation (Chardonnay) against a chosen
# Libation files folder.
#
# LIBATION_FILES_DIR takes precedence over appsettings.json, and is scoped to this
# process only - your installed Libation is unaffected.
#
# Usage:
#   .\run-dev.ps1                       # uses the clean library
#   .\run-dev.ps1 -FilesDir "E:\_Libations"   # or point somewhere else

param(
	[string]$FilesDir = "E:\_LibationClean"
)

if (-not (Test-Path $FilesDir)) {
	Write-Error "Libation files folder not found: $FilesDir"
	Write-Host "Libation silently ignores LIBATION_FILES_DIR when the folder doesn't exist," -ForegroundColor Yellow
	Write-Host "and would fall back to appsettings.json - which is rarely what you want." -ForegroundColor Yellow
	exit 1
}

$env:LIBATION_FILES_DIR = $FilesDir

Write-Host "Libation files: $FilesDir" -ForegroundColor Cyan
Write-Host "Starting dev build..." -ForegroundColor Cyan

Push-Location (Join-Path $PSScriptRoot "Source\LibationAvalonia")
try {
	dotnet run
}
finally {
	Pop-Location
}
