# Opens the project in batchmode (import + compile), runs project setup, and reports compiler errors.
# Usage: powershell -File scripts/compile.ps1 [-Setup]
param([switch]$Setup)
. (Join-Path $PSScriptRoot 'common.ps1')

$args2 = @('-quit')
if ($Setup) { $args2 += @('-executeMethod', 'LumenWave.EditorTools.ProjectSetup.RunBatch') }
$code = Invoke-UnityBatch -UnityArgs $args2 -LogName "compile-$PID.log"
$n = Show-CompileErrors "compile-$PID.log"
$log = Join-Path $LW_Logs "compile-$PID.log"
Select-String -Path $log -Pattern '\[LumenWave\]' | ForEach-Object { Write-Host $_.Line.Trim() }
if ($n -gt 0) { Write-Host "COMPILE FAILED: $n error(s). Log: $log"; exit 1 }
if ($code -ne 0) { Write-Host "Unity exited with $code (see $log)"; exit $code }
Write-Host 'COMPILE OK'
