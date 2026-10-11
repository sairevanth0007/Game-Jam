# Builds all level scenes from data, then the Windows player, then zips it to Release/.
# Usage: powershell -File scripts/build.ps1 [-NoZip] [-Name Windows] [-ZipName LumenWave-Windows-visual.zip]
# -Name builds into Builds/<Name>/ so parallel workers don't overwrite a player that is running.
param([switch]$NoZip, [string]$Name = 'Windows', [string]$ZipName = 'LumenWave-Windows-visual.zip')
. (Join-Path $PSScriptRoot 'common.ps1')

$code = Invoke-UnityBatch -UnityArgs @('-quit', '-executeMethod', 'LumenWave.EditorTools.BuildScript.BuildWindowsBatch', '-lw-build-path', "Builds/$Name/LumenWave.exe") -LogName "build-$Name.log"
$n = Show-CompileErrors "build-$Name.log"
Select-String -Path (Join-Path $LW_Logs "build-$Name.log") -Pattern '\[LumenWave\]' | ForEach-Object { Write-Host $_.Line.Trim() }
$exe = Join-Path $LW_Root "Builds\$Name\LumenWave.exe"
if ($n -gt 0 -or $code -ne 0 -or -not (Test-Path $exe)) { Write-Host "BUILD FAILED (exit $code). See Logs/build-$Name.log"; exit 1 }
Write-Host "BUILD OK: $exe"
if (-not $NoZip) {
    $rel = Join-Path $LW_Root 'Release'
    if (-not (Test-Path $rel)) { New-Item -ItemType Directory $rel | Out-Null }
    $zip = Join-Path $rel $ZipName
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $LW_Root "Builds\$Name\*") -DestinationPath $zip
    $mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "ZIP: $zip ($mb MB)"
}
