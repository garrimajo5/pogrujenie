# Publish a QA report: copy screenshots from the last run into the report folder, commit and push qa/reports.
# Usage: publish-qa.ps1 -Folder <ГГГГ-ММ-ДД-ЧЧММ-name> -Message "<commit message>"
# report.md must already be written to qa\<Folder>\report.md.
param([Parameter(Mandatory)][string]$Folder, [Parameter(Mandatory)][string]$Message)
$ErrorActionPreference = 'Stop'
$root = 'D:\AI\qa-reports'
$dir = Join-Path $root "qa\$Folder"
if (-not (Test-Path "$dir\report.md")) { throw "No report.md in $dir" }
Copy-Item 'D:\AI\qa-build\run\*.png' $dir -Force
git -C $root add -A "qa/$Folder"
git -C $root -c user.name=garrimajo5 -c user.email=garrimajo5@users.noreply.github.com commit -q -m $Message -m 'Co-Authored-By: Claude <noreply@anthropic.com>'
git -C $root push -q origin qa/reports
Write-Output "PUSHED https://github.com/garrimajo5/pogrujenie/tree/qa/reports/qa/$Folder"
