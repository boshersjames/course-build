# Runs the course automation in Unity batch mode (Unity must be closed), then prints the summary.
#   .\automation\Run-CourseAutomation.ps1                         # everything enabled in the Tierra config
#   .\automation\Run-CourseAutomation.ps1 -Steps "Plant shrubs,Paint grass"
#   .\automation\Run-CourseAutomation.ps1 -Steps none             # just check the scene and take screenshots
#   .\automation\Run-CourseAutomation.ps1 -SyncTools              # copy the repo's editor tools into the project first
# If Unity is open, use Send-UnityCommand.ps1 instead (the live bridge).
param(
    [string]$Project = "C:\GSP\GSPBaseProject_v4_2025_12_02",
    [string]$Config = "",
    [string]$Steps = "",
    [switch]$NoShots,
    [switch]$SyncTools,
    [string]$Unity = ""
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Config) { $Config = Join-Path $PSScriptRoot "configs\TierraRejada.json" }
$Config = (Resolve-Path $Config).Path

if (-not $Unity) {
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\2018.2.8f1\Editor\Unity.exe",
        "C:\Program Files\Unity 2018.2.8f1\Editor\Unity.exe",
        "C:\Program Files\Unity\Editor\Unity.exe",
        "C:\Program Files (x86)\Unity\Editor\Unity.exe"
    )
    $Unity = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $Unity) { throw "Unity 2018.2.8f1 not found. Pass -Unity 'C:\path\to\Unity.exe'." }
}

# Unity holds Temp\UnityLockfile open while the project is open
$lock = Join-Path $Project "Temp\UnityLockfile"
if (Test-Path $lock) {
    try { $s = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $s.Close() }
    catch { throw "Unity has the project open. Close it, or use .\automation\Send-UnityCommand.ps1 run" }
}

if ($SyncTools) {
    $editor = Join-Path $Project "Assets\Editor"
    Get-ChildItem (Join-Path $repo "unity_editor\*.cs") | ForEach-Object {
        Copy-Item $_.FullName $editor -Force
        Write-Host "copied $($_.Name) -> Assets\Editor"
    }
    $cfgDir = Join-Path $Project "Assets\CourseAutomation"
    New-Item -ItemType Directory -Force $cfgDir | Out-Null
    Copy-Item (Join-Path $PSScriptRoot "configs\*.json") $cfgDir -Force
}

$out = Join-Path $Project "AutomationOutput"
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out "unity_batch.log"
$unityArgs = @("-batchmode", "-projectPath", "`"$Project`"", "-executeMethod", "CourseAutomation.RunBatch",
          "-automationConfig", "`"$Config`"", "-logFile", "`"$log`"")
if ($Steps) { $unityArgs += @("-automationSteps", "`"$Steps`"") }
if ($NoShots) { $unityArgs += "-automationNoShots" }

$latest = Join-Path $out "latest.txt"
$before = if (Test-Path $latest) { Get-Content $latest } else { "" }
Write-Host "Running Unity in batch mode (log: $log) ..."
$t0 = Get-Date
$p = Start-Process -FilePath $Unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
Write-Host ("Unity exited with code {0} after {1:n0} s" -f $p.ExitCode, ((Get-Date) - $t0).TotalSeconds)

$now = if (Test-Path $latest) { Get-Content $latest } else { "" }
if ($now -and $now -ne $before) {
    Get-Content (Join-Path $now "summary.md")
    Write-Host ""
    Write-Host "Shots: $now\index.html"
} else {
    Write-Host "No summary was written. Compile errors or a crash: last lines of the log:"
    Get-Content $log -Tail 40
}
exit $p.ExitCode
