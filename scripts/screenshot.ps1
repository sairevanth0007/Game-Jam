# Launches the built player in screenshot mode. It visits each shot listed in
# Assets/LumenWave/Resources/screenshots.txt (or -Shots "level:x:y;..."), saves PNGs to Screenshots/ and quits.
# Usage: powershell -File scripts/screenshot.ps1 [-Shots "1:5:2;3:20:4"] [-Width 1600 -Height 900]
param([string]$Shots = '', [int]$Width = 1600, [int]$Height = 900, [int]$TimeoutSec = 240)
. (Join-Path $PSScriptRoot 'common.ps1')

$exe = Join-Path $LW_Root 'Builds\Windows\LumenWave.exe'
if (-not (Test-Path $exe)) { Write-Host "No build at $exe - run scripts/build.ps1 first"; exit 1 }
$out = Join-Path $LW_Root 'Screenshots'
if (-not (Test-Path $out)) { New-Item -ItemType Directory $out | Out-Null }
$log = Join-Path $LW_Logs 'screenshot-player.log'
$a = @('-screen-fullscreen', '0', '-screen-width', "$Width", '-screen-height', "$Height", '-logFile', $log, '-lw-screenshots', $out)
if ($Shots) { $a += @('-lw-shots', $Shots) }
$p = Start-Process -FilePath $exe -ArgumentList ($a | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); Write-Host 'Player timed out (killed)'; }
Select-String -Path $log -Pattern '\[LumenWave\]|Exception' | ForEach-Object { Write-Host $_.Line.Trim() }
Get-ChildItem $out -Filter *.png | Sort-Object LastWriteTime -Descending | Select-Object -First 30 | ForEach-Object { Write-Host $_.FullName }
