# Runs the bot on the built player for each level and prints the fps + completion lines.
# Usage: powershell -File scripts/autoplay.ps1 [-Name visual] [-Levels "1,2,3"] [-Seconds 90]
param([string]$Name = 'Windows', [string]$Levels = '1,2,3,4,5,6,7,8,9,10', [int]$Seconds = 120)
. (Join-Path $PSScriptRoot 'common.ps1')

$exe = Join-Path $LW_Root "Builds\$Name\LumenWave.exe"
if (-not (Test-Path $exe)) { Write-Host "No build at $exe"; exit 1 }
$fails = 0
foreach ($l in ($Levels -split '[,; ]+' | Where-Object { $_ } | ForEach-Object { [int]$_ })) {
    $dir = Join-Path $env:TEMP ("lw_autoplay_{0}_L{1}_{2}" -f $Name, $l, (Get-Date -Format 'HHmmss'))
    New-Item -ItemType Directory -Force $dir | Out-Null
    $log = Join-Path $LW_Logs "autoplay-$Name-L$l.log"
    $a = @('-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-logFile', $log,
           '-lw-autoplay', $dir, '-lw-autoplay-level', "$l", '-lw-autoplay-seconds', "$Seconds")
    $p = Start-Process -FilePath $exe -ArgumentList ($a | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -PassThru
    if (-not $p.WaitForExit(($Seconds + 60) * 1000)) { $p.Kill(); Write-Host "L$l timed out" }
    $lines = Select-String -Path $log -ErrorAction SilentlyContinue -Pattern 'Autoplay frames|Autoplay DONE|Exception' | ForEach-Object { $_.Line.Trim() }
    $lines | ForEach-Object { Write-Host $_ }
    if (-not ($lines -match 'completed=True') -or -not ($lines -match 'deaths=0') -or ($lines -match 'Exception')) { $fails++ }
}
Write-Host "AUTOPLAY FAILS: $fails"
