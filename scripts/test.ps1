# Runs Unity tests in batchmode. Usage: powershell -File scripts/test.ps1 [-Platform EditMode|PlayMode|All] [-Filter <regex>]
param([ValidateSet('EditMode', 'PlayMode', 'All')][string]$Platform = 'All', [string]$Filter = '')
. (Join-Path $PSScriptRoot 'common.ps1')

$platforms = if ($Platform -eq 'All') { @('EditMode', 'PlayMode') } else { @($Platform) }
$failed = 0
foreach ($pf in $platforms) {
    $xml = Join-Path $LW_Logs "results-$pf-$PID.xml"
    if (Test-Path $xml) { Remove-Item $xml -Force }
    $a = @('-runTests', '-testPlatform', $pf, '-testResults', $xml)
    if ($Filter) { $a += @('-testFilter', $Filter) }
    # PlayMode tests need graphics off too; rendering is not required for logic tests.
    $code = Invoke-UnityBatch -UnityArgs $a -LogName "test-$pf-$PID.log"
    $n = Show-CompileErrors "test-$pf-$PID.log"
    if (-not (Test-Path $xml)) { Write-Host "$pf : NO RESULTS (exit $code, compile errors $n). See Logs/test-$pf-$PID.log"; $failed++; continue }
    [xml]$r = Get-Content $xml
    $run = $r.'test-run'
    Write-Host ("{0}: total={1} passed={2} failed={3} skipped={4}" -f $pf, $run.total, $run.passed, $run.failed, $run.skipped)
    $r.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        Write-Host ("  FAIL {0}: {1}" -f $_.fullname, ($_.failure.message.'#cdata-section' -replace '\s+', ' '))
    }
    if ([int]$run.failed -gt 0) { $failed++ }
}
if ($failed -gt 0) { exit 1 }
