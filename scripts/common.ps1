# Shared helpers for LumenWave scripts. Dot-source this file.
# Serialises all Unity batchmode runs with a lock directory so parallel workers never
# open the project twice (Unity refuses a second instance on the same project).

$ErrorActionPreference = 'Stop'
$Global:LW_Root  = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Global:LW_Unity = 'C:\Program Files\Unity\Hub\Editor\6000.2.9f1\Editor\Unity.exe'
$Global:LW_Logs  = Join-Path $LW_Root 'Logs'
if (-not (Test-Path $LW_Logs)) { New-Item -ItemType Directory -Force $LW_Logs | Out-Null }

function Enter-UnityLock([int]$TimeoutMinutes = 40) {
    $lock = Join-Path $LW_Root '.unitylock'
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ($true) {
        try {
            New-Item -ItemType Directory -Path $lock -ErrorAction Stop | Out-Null
            Set-Content -Path (Join-Path $lock 'owner.txt') -Value "$PID $(Get-Date -Format o)"
            # Belt and braces: also wait for any Unity editor still open on this project
            # (e.g. a run whose lock was removed by hand).
            while (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
                   Where-Object { $_.CommandLine -and $_.CommandLine -like '*-projectPath*Game-Jam*' }) {
                if ((Get-Date) -gt $deadline) { Remove-Item -Recurse -Force $lock -ErrorAction SilentlyContinue; throw 'Timed out waiting for running Unity' }
                Start-Sleep -Seconds 5
            }
            return
        } catch {
            # Stale lock: older than 30 min and no Unity batch process alive.
            $age = (Get-Date) - (Get-Item $lock).CreationTime
            $unity = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
                     Where-Object { $_.CommandLine -and $_.CommandLine -like '*-projectPath*Game-Jam*' }
            if ($age.TotalMinutes -gt 30 -and -not $unity) {
                Remove-Item -Recurse -Force $lock -ErrorAction SilentlyContinue
                continue
            }
            if ((Get-Date) -gt $deadline) { throw "Timed out waiting for Unity lock ($lock)" }
            Start-Sleep -Seconds 5
        }
    }
}

function Exit-UnityLock {
    $lock = Join-Path $LW_Root '.unitylock'
    Remove-Item -Recurse -Force $lock -ErrorAction SilentlyContinue
}

# Runs Unity in batchmode with the given extra args. Returns the exit code.
function Invoke-UnityBatch([string[]]$UnityArgs, [string]$LogName, [switch]$Graphics, [int]$TimeoutMinutes = 45) {
    $log = Join-Path $LW_Logs $LogName
    $all = @('-batchmode', '-projectPath', $LW_Root, '-logFile', $log)
    if (-not $Graphics) { $all += '-nographics' }
    $all += $UnityArgs
    Enter-UnityLock
    try {
        if (Test-Path $log) { Remove-Item $log -Force }
        # Note: Start-Process -Wait also waits for Unity's lingering child processes (licensing client,
        # bee backend) and can hang forever, so wait on the Unity process itself.
        $p = Start-Process -FilePath $LW_Unity -ArgumentList ($all | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -PassThru -NoNewWindow
        $null = $p.Handle  # cache handle so ExitCode is available after exit
        if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
            Write-Host "Unity timed out after $TimeoutMinutes min - killing"
            $p.Kill()
            return 124
        }
        return $p.ExitCode
    } finally {
        Exit-UnityLock
    }
}

# Prints compiler errors (unique) from a Unity log. Returns count.
function Show-CompileErrors([string]$LogName) {
    $log = Join-Path $LW_Logs $LogName
    if (-not (Test-Path $log)) { Write-Host "No log $log"; return 1 }
    $errs = Select-String -Path $log -Pattern 'error CS\d+' | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
    foreach ($e in $errs) { Write-Host $e }
    return @($errs).Count
}
