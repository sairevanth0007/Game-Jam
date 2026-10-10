# Builds all level scenes from data, then the Windows player, then zips it to Release/.
# Usage: powershell -File scripts/build.ps1 [-NoZip]
param([switch]$NoZip)
. (Join-Path $PSScriptRoot 'common.ps1')

$code = Invoke-UnityBatch -UnityArgs @('-quit', '-executeMethod', 'LumenWave.EditorTools.BuildScript.BuildWindowsBatch') -LogName 'build.log'
$n = Show-CompileErrors 'build.log'
Select-String -Path (Join-Path $LW_Logs 'build.log') -Pattern '\[LumenWave\]' | ForEach-Object { Write-Host $_.Line.Trim() }
$exe = Join-Path $LW_Root 'Builds\Windows\LumenWave.exe'
if ($n -gt 0 -or $code -ne 0 -or -not (Test-Path $exe)) { Write-Host "BUILD FAILED (exit $code). See Logs/build.log"; exit 1 }
Write-Host "BUILD OK: $exe"
if (-not $NoZip) {
    $rel = Join-Path $LW_Root 'Release'
    if (-not (Test-Path $rel)) { New-Item -ItemType Directory $rel | Out-Null }
    $zip = Join-Path $rel 'LumenWave-Windows.zip'
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $LW_Root 'Builds\Windows\*') -DestinationPath $zip
    $mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "ZIP: $zip ($mb MB)"
}
