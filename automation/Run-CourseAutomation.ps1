# Runs the course automation in Unity batch mode (Unity must be closed), then prints the summary.
# Course-agnostic: run it from a course folder (one with course.json) or pass -CourseJson; the Unity project path and the
# automation config come from course.json (paths.unity_project, automation/<Name>.json) unless overridden.
#   .\automation\Run-CourseAutomation.ps1                         # everything enabled in the course config
#   .\automation\Run-CourseAutomation.ps1 -Steps "Plant shrubs,Paint grass"
#   .\automation\Run-CourseAutomation.ps1 -Steps none             # just check the scene and take screenshots
#   .\automation\Run-CourseAutomation.ps1 -SyncTools              # copy the toolkit's editor tools, course.json and rules into the project first
#   .\automation\Run-CourseAutomation.ps1 -SyncTools -SyncOnly    # the copy alone, Unity may stay open; then Send-UnityCommand.ps1 refresh + ping
# If Unity is open, use Send-UnityCommand.ps1 instead (the live bridge).
param(
    [string]$Project = "",
    [string]$CourseJson = "",
    [string]$Config = "",
    [string]$Steps = "",
    [switch]$NoShots,
    [switch]$SyncTools,
    [switch]$SyncOnly,      # with -SyncTools: copy and stop, no batch run; works with Unity open (the copy touches no locked file)
    [string]$Unity = ""
)
$ErrorActionPreference = "Stop"
$toolkit = Split-Path -Parent $PSScriptRoot
# course.json: -CourseJson, else $env:COURSE_JSON, else walk up from the current folder
if (-not $CourseJson) { $CourseJson = $env:COURSE_JSON }
if (-not $CourseJson) { $d = (Get-Location).Path; while ($d -and -not (Test-Path (Join-Path $d "course.json"))) { $d = Split-Path -Parent $d }; if ($d) { $CourseJson = Join-Path $d "course.json" } }
if (-not $CourseJson -or -not (Test-Path $CourseJson)) { throw "course.json not found: run from the course folder, set COURSE_JSON, or pass -CourseJson" }
$course = Get-Content $CourseJson -Raw | ConvertFrom-Json
$courseDir = Split-Path -Parent (Resolve-Path $CourseJson).Path
if (-not $Project) { $Project = $course.paths.unity_project -replace '/', '\' }
if (-not $Config) {
    $Config = Join-Path $courseDir ("automation\" + $course.name + ".json")
    if (-not (Test-Path $Config)) { $Config = Join-Path $PSScriptRoot ("configs\" + $course.name + ".json") }
}
$Config = (Resolve-Path $Config).Path
Write-Host "course $($course.name): project $Project, config $Config"

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

if ($SyncTools) {
    $editor = Join-Path $Project "Assets\Editor"
    New-Item -ItemType Directory -Force $editor | Out-Null
    Get-ChildItem (Join-Path $toolkit "unity\shared\*.cs") | ForEach-Object { Copy-Item $_.FullName $editor -Force; Write-Host "copied $($_.Name) -> Assets\Editor" }
    $res = Join-Path $editor "Resources"; if (-not (Test-Path $res)) { New-Item -ItemType Directory -Path $res | Out-Null }
    Get-ChildItem (Join-Path $toolkit "unity\shared\*.shader") | ForEach-Object { Copy-Item $_.FullName $res -Force; Write-Host "copied $($_.Name) -> Assets\Editor\Resources" }
    # the course's own editor scripts, if it has any
    $courseEditor = Join-Path $courseDir "unity_editor"
    if (Test-Path $courseEditor) { Get-ChildItem (Join-Path $courseEditor "*.cs") | ForEach-Object {
        if (Test-Path (Join-Path $toolkit ("unity\shared\" + $_.Name))) { Write-Host "SKIPPED $($_.Name) (course copy shadows the toolkit's unity/shared version; delete it from unity_editor)" -ForegroundColor Yellow }
        else { Copy-Item $_.FullName $editor -Force; Write-Host "copied $($_.Name) (course) -> Assets\Editor" } } }
    $cfgDir = Join-Path $Project "Assets\CourseAutomation"
    New-Item -ItemType Directory -Force $cfgDir | Out-Null
    Copy-Item $Config $cfgDir -Force
    # course.json (with the absolute course folder added) and the rule files, for CourseGreenChecks and friends
    $cb = Join-Path $Project "Assets\CourseBuild"; New-Item -ItemType Directory -Force (Join-Path $cb "rules") | Out-Null
    $cj = Get-Content $CourseJson -Raw | ConvertFrom-Json
    $cj.paths | Add-Member -NotePropertyName course_repo_abs -NotePropertyValue $courseDir -Force
    $cj | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $cb "course.json") -Encoding UTF8
    Copy-Item (Join-Path $toolkit "rules\*.json") (Join-Path $cb "rules") -Force
    Write-Host "synced course.json + rules -> Assets\CourseBuild"
    if ($SyncOnly) { Write-Host "sync only: done (Unity may be open; run Send-UnityCommand.ps1 refresh, then ping)"; exit 0 }
}



# Unity holds Temp\UnityLockfile open while the project is open
$lock = Join-Path $Project "Temp\UnityLockfile"
if (Test-Path $lock) {
    try { $s = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $s.Close() }
    catch { throw "Unity has the project open. Close it, or use .\automation\Send-UnityCommand.ps1 run" }
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
