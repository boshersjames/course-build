<#
Install-PlaytestBuild.ps1  -  back up the Tierra GSPro course, build the course in editor C from A's saved scene,
install the new TierraRejada.gspcrse into GSPro and verify it.

DRY RUN IS THE DEFAULT. Nothing is copied, built or installed unless you pass -Go.

    .\automation\Install-PlaytestBuild.ps1                 # dry run: checks + prints every step
    .\automation\Install-PlaytestBuild.ps1 -Go             # backup, sync A->C, build in C (staged), install, verify
    .\automation\Install-PlaytestBuild.ps1 -Go -SkipSync   # C already holds A's saved scene
    .\automation\Install-PlaytestBuild.ps1 -Go -BuildInPlace   # old way (Oct 27): C writes straight into the GSPro folder
    .\automation\Install-PlaytestBuild.ps1 -Go -InstallGkd .\greenkeeper\TierraRejada.GKD   # also install a GKD

How the build works (found Oct 31):
  * GSP > Build Course (Assets\Editor\BuildCourse.cs) is an EditorWindow: the build only runs from its "Build Course"
    button, so the menu item can't be scripted. The button calls BuildCourseDLL.Encryption (DisableTerrains,
    BuildBundle, CleanUpDirectory) and writes <SaveLocation>\<SceneName>\<SceneName>.gspcrse, where SaveLocation is
    Assets\Resources\OPCDThemes\Store\BuildCourseSaveLocation.asset (= C:/GSProV1/Core/GSP/Courses in A and C).
  * Assets\Editor\TmpBuild.cs (Tools > TmpBuild Build) does the same calls without the dialogs, with every terrain
    collider toggle on ("disable collider" boxes checked). It was used for the Oct 27 22:23 publish and writes
    C:\GSP\GSPBaseProject_v4_2025_12_02\AutomationOutput\build_result.txt. We call it through the live bridge.
  * By default this script points C's save location at a staging folder first, so Unity never writes into the
    GSPro tree; the script copies the finished file in after checking it. The next Sync-EditorC run restores the
    asset from A anyway; this script also puts it back after the build.
  * The bundle is built from the scene FILE on disk: only what A has saved is in the build.

Rules: A must not save while the A->C robocopy runs (checked below: the script aborts if A's scene changes during
the sync). Never save editor C after the build (DisableTerrains switched its terrain colliders off in memory).
#>
[CmdletBinding()]
param(
    [switch]$Go,                                    # really do it (otherwise dry run)
    [switch]$AllowConnect,                          # tolerate GSPconnect.exe (launch-monitor bridge; it doesn't open course files). GSPro/Greenkeeper must still be closed
    [switch]$SkipSync,                              # don't run Sync-EditorC.ps1 (C already synced)
    [switch]$SkipBuild,                             # install the file already in the staging folder
    [switch]$BuildOnly,                             # sync + build into staging and run the size checks, then stop: no backup, no GSPro check, nothing written to the course folder (GSPro may stay open)
    [double]$MaxGrowth = 1.6,                        # size guard: new build may be at most this many times the old one (a day of heavy edits can pass 1.6x)
    [switch]$BuildInPlace,                          # let C write straight into the GSPro course folder (no staging)
    [string]$InstallGkd = "",                       # optional: GKD to install as TierraRejada.GKD (e.g. the repo one)
    [string]$A = 'C:\GSP\GSPBaseProject_v4_2025_12_02',
    [string]$C = 'D:\GSP\GSPBaseProject_v4_2025_12_02_C',
    [string]$Course = 'TierraRejada',
    [string]$CoursesRoot = 'C:\GSProV1\Core\GSP\Courses',
    [string]$BackupRoot = 'C:\Users\user\Documents\tierra-rejada\gspro_backups',
    [string]$StageRoot = 'D:\GSP\playtest_stage',
    [int]$BuildTimeoutSec = 3600
)
$ErrorActionPreference = 'Stop'
$Repo      = Split-Path $PSScriptRoot -Parent
$Bridge    = Join-Path $PSScriptRoot 'Send-UnityCommand.ps1'
$SyncC     = Join-Path $PSScriptRoot 'Sync-EditorC.ps1'
$CourseDir = Join-Path $CoursesRoot $Course
$Crse      = Join-Path $CourseDir "$Course.gspcrse"
$Gkd       = Join-Path $CourseDir "$Course.GKD"
$SaveLocAsset = Join-Path $C 'Assets\Resources\OPCDThemes\Store\BuildCourseSaveLocation.asset'
$BuildResult  = Join-Path $A 'AutomationOutput\build_result.txt'     # TmpBuild writes here even when run in C
$StageCrse    = Join-Path $StageRoot "$Course\$Course.gspcrse"
$Stamp     = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $BackupRoot $Stamp
$T0        = Get-Date

# ------------------------------------------------------------------ helpers
function Say($msg, $color = 'Gray') { Write-Host $msg -ForegroundColor $color }
function Step($msg) { Write-Host ""; Write-Host "== $msg" -ForegroundColor Cyan }
function Fail($msg) { Write-Host ""; Write-Host "ABORT: $msg" -ForegroundColor Red; Show-Rollback; exit 1 }
function Act([string]$desc, [scriptblock]$action) {
    # Runs $action only with -Go; otherwise just says what it would do.
    if ($Go) { Say "  -> $desc"; & $action } else { Say "  [dry-run] would: $desc" 'Yellow' }
}
function Bridge([string]$cmd, [int]$timeout = 120, [hashtable]$extra = @{}) {
    # One live-bridge command to editor C. Returns the text; throws if the bridge didn't answer [ok].
    $global:LASTEXITCODE = 0   # robocopy (backup / sync) leaves 1 = "files copied"; a stale code made every [ok] answer count as a failure
    $out = & $Bridge $cmd -Project $C -TimeoutSec $timeout @extra 6>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $out -notmatch '\[ok\]') { throw "bridge '$cmd' failed:`n$out" }
    return $out
}
function SetSaveLocation([string]$value) {
    # Rewrites the GSPCoursesDirectory line of C's BuildCourseSaveLocation.asset (UTF-8 without BOM: Unity YAML).
    $txt = [IO.File]::ReadAllText($SaveLocAsset)
    $txt = $txt -replace '(?m)^([ \t]*GSPCoursesDirectory:[ \t]*)[^\r\n]*', ('${1}' + $value.Replace('$', '$$'))
    [IO.File]::WriteAllText($SaveLocAsset, $txt, (New-Object Text.UTF8Encoding($false)))
}
function WaitPing([int]$tries = 60) {
    for ($i = 0; $i -lt $tries; $i++) {
        try { Bridge 'ping' 10 | Out-Null; return } catch { Start-Sleep 5 }
    }
    throw "editor C doesn't answer ping (compiling, or a dialog is open in C?)"
}
function GsproProcesses {
    # GSPro, its launcher/connect app, Greenkeeper, or anything else running from C:\GSProV1.
    Get-Process | Where-Object {
        $_.Name -in @('GSPro', 'GSPLauncher', 'GSPconnect', 'GreenKeeper', 'GSProConnectRestoreFiles') -or
        ($_.Path -and $_.Path -like 'C:\GSProV1\*')
    } | Where-Object { -not ($AllowConnect -and $_.Name -eq 'GSPconnect')
    }
}
function AssertGsproClosed {
    if ($BuildOnly) { Say '  -BuildOnly: GSPro may run (nothing goes to the course folder)' 'Yellow'; return }
    $p = @(GsproProcesses)
    if ($p.Count -gt 0) { Fail ("GSPro/Greenkeeper is running (" + (($p | ForEach-Object { "$($_.Name) pid $($_.Id)" }) -join ', ') + "). Close it yourself and rerun; this script never kills it.") }
    Say "  GSPro / Greenkeeper not running" 'Green'
}
function FileTable($dir) {
    # name -> size + SHA256 for every file under $dir (relative paths)
    $t = @{}
    Get-ChildItem $dir -Recurse -File -Force | ForEach-Object {
        $rel = $_.FullName.Substring($dir.TrimEnd('\').Length + 1)
        $t[$rel] = [pscustomobject]@{ Size = $_.Length; Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash; Time = $_.LastWriteTime }
    }
    return $t
}
function Show-Rollback {
    if (-not (Test-Path (Join-Path $BackupDir 'course'))) { return }
    Write-Host ""
    Write-Host "ROLLBACK (GSPro closed): restores every course file from the backup:" -ForegroundColor Magenta
    Write-Host "  Copy-Item -Path '$BackupDir\course\*' -Destination '$CourseDir' -Recurse -Force" -ForegroundColor Magenta
    Write-Host "  (then check: Get-FileHash '$Crse' should equal the hash in '$BackupDir\manifest.csv')" -ForegroundColor Magenta
}

Say ("Install-PlaytestBuild  " + $(if ($Go) { 'LIVE RUN (-Go)' } else { 'DRY RUN (add -Go to do it)' })) $(if ($Go) { 'Red' } else { 'Yellow' })
Say "  course folder $CourseDir"
Say "  build in      C = $C (from A = $A)  ->  $(if ($BuildInPlace) { 'straight into the course folder' } else { "staged at $StageCrse" })"
Say "  backup to     $BackupDir"

# ------------------------------------------------------------------ 0. preflight (read-only)
Step "0. Preflight"
foreach ($p in @($CourseDir, $Crse, $Gkd, $A, $C, $Bridge, $SyncC, $SaveLocAsset)) { if (-not (Test-Path $p)) { Fail "missing: $p" } }
AssertGsproClosed
$gkdJson = Get-Content $Gkd -Raw | ConvertFrom-Json
if ($gkdJson.SceneFolderName -ne $Course) { Fail "GKD SceneFolderName is '$($gkdJson.SceneFolderName)', expected '$Course' (GSPro loads <SceneFolderName>\<SceneFolderName>.gspcrse)" }
Say "  GKD SceneFolderName = $($gkdJson.SceneFolderName)  -> GSPro loads $Course\$Course.gspcrse" 'Green'
$old = Get-Item $Crse
Say ("  current .gspcrse: {0:N0} bytes, {1}" -f $old.Length, $old.LastWriteTime)
$unityC = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*_C *' }
if (-not $unityC -and -not $SkipBuild) { Fail "editor C (title '..._C - PC...') isn't open" }
if (-not (Test-Path (Join-Path $A 'Assets\Editor\TmpBuild.cs'))) {
    Fail ("A has no Assets\Editor\TmpBuild.cs, so the build can't be scripted. Manual build in C (or A):`n" +
          "  1. Open the saved scene Assets/TierraRejada.unity.`n  2. GSP > Build Course. Course Directory should read C:/GSProV1/Core/GSP/Courses.`n" +
          "  3. Drag TierraRejada.unity into 'Scene File'; leave every 'Disable <terrain> Collider' box checked.`n" +
          "  4. Click Build Course; answer 'Course Update - Continue' / 'Ignore and Build' only if you've read the message.`n" +
          "  5. Wait for 'Course Build Completed' in the Console; it writes $Crse directly.")
}
$needGB = [math]::Ceiling(($old.Length * 3) / 1GB) + 1
foreach ($drv in @('C', 'D')) { $free = (Get-PSDrive $drv).Free / 1GB; if ($free -lt $needGB) { Fail "drive ${drv}: only $([math]::Round($free,1)) GB free" } }
Say "  disk space OK (>= $needGB GB free on C: and D:)" 'Green'

# ------------------------------------------------------------------ 1. backup + verify
Step "1. Back up the whole course folder (GKD included) to $BackupDir"
if ($BuildOnly -and $BuildInPlace) { Fail '-BuildOnly needs the staging folder (no -BuildInPlace)' }
if (-not $BuildOnly) {
Act "create $BackupDir (and a .gitignore so the 1 GB backup never gets committed)" {
    New-Item -ItemType Directory -Force (Join-Path $BackupDir 'course') | Out-Null
    $gi = Join-Path $BackupRoot '.gitignore'; if (-not (Test-Path $gi)) { Set-Content -Encoding ASCII $gi '*' }
}
Act "copy $CourseDir\* -> $BackupDir\course\ and verify count, sizes and SHA256" {
    Copy-Item -Path "$CourseDir\*" -Destination (Join-Path $BackupDir 'course') -Recurse -Force
    $src = FileTable $CourseDir; $dst = FileTable (Join-Path $BackupDir 'course')
    if ($src.Count -ne $dst.Count) { Fail "backup has $($dst.Count) files, course folder has $($src.Count)" }
    foreach ($k in $src.Keys) {
        if (-not $dst.ContainsKey($k)) { Fail "backup is missing $k" }
        if ($src[$k].Size -ne $dst[$k].Size -or $src[$k].Hash -ne $dst[$k].Hash) { Fail "backup copy of $k doesn't match" }
    }
    $script:Before = $src
    $src.GetEnumerator() | ForEach-Object { [pscustomobject]@{ File = $_.Key; Size = $_.Value.Size; SHA256 = $_.Value.Hash; LastWrite = $_.Value.Time } } |
        Export-Csv -NoTypeInformation (Join-Path $BackupDir 'manifest.csv')
    Copy-Item $Gkd (Join-Path $BackupDir "$Course.GKD")    # loose GKD copy, easy to find
    Say "  backup verified: $($src.Count) files, hashes match" 'Green'
}
} else { Say '  -BuildOnly: no backup (the course folder is not touched)' 'Yellow' }

# ------------------------------------------------------------------ 2. GSPro must be closed
Step "2. GSPro check"
AssertGsproClosed

# ------------------------------------------------------------------ 3. sync A -> C and build in C
Step "3. Build in editor C"
if (-not $SkipBuild) {
    if (-not $SkipSync) {
        $sceneA = Join-Path $A 'Assets\TierraRejada.unity'; $sceneC = Join-Path $C 'Assets\TierraRejada.unity'
        Say "  A's scene saved $((Get-Item $sceneA).LastWriteTime). DON'T SAVE IN A until the sync says done." 'Yellow'
        Act "run Sync-EditorC.ps1 (robocopy /MIR A\Assets -> C\Assets, reload, refresh, open scene)" {
            $hA0 = (Get-FileHash $sceneA).Hash; $tA0 = (Get-Item $sceneA).LastWriteTime
            & $SyncC -A $A -C $C | ForEach-Object { Say "    $_" }
            if ((Get-Item $sceneA).LastWriteTime -ne $tA0) { Fail "A's scene was saved during the sync; rerun when A is idle" }
            if ((Get-FileHash $sceneC).Hash -ne $hA0) { Fail "C's TierraRejada.unity differs from A's after the sync" }
            WaitPing
            Say "  C holds A's saved scene (hash match), bridge answers" 'Green'
        }
    }
    else { Say "  -SkipSync: using what C has now (C scene $((Get-Item (Join-Path $C 'Assets\TierraRejada.unity')).LastWriteTime))" 'Yellow' }

    $origLoc = (Select-String -Path $SaveLocAsset -Pattern '^\s*GSPCoursesDirectory:\s*(.*)$').Matches[0].Groups[1].Value.Trim()
    $expectLoc = if ($BuildInPlace) { $origLoc } else { $StageRoot.Replace('\', '/') }
    if (-not $BuildInPlace) {
        Act "point C's BuildCourseSaveLocation at $expectLoc (was $origLoc), clear the old staged build, refresh C" {
            $stageCourse = Join-Path $StageRoot $Course
            if (Test-Path $stageCourse) { Move-Item $stageCourse "$stageCourse.old_$Stamp" }
            New-Item -ItemType Directory -Force $stageCourse | Out-Null
            SetSaveLocation $expectLoc
            Bridge 'refresh' 600 | Out-Null; WaitPing
        }
    }
    else { Say "  -BuildInPlace: Unity will overwrite $Crse during the build (backup above is the safety net)" 'Yellow' }

    # placement gate (BoJa Nov 1: nothing planted or moved onto play surfaces without his OK): crowns 1 m+ over a green or collar block the
    # build unless listed in AutomationOutput\placement_exceptions.txt ("root/name x z", BoJa-approved); other overhangs are printed as warnings
    Act "placement gate in C (TierraNight1031.PlacementGate)" {
        $g = @(Bridge 'invoke' 2400 @{ Tool = 'TierraNight1031'; Method = 'PlacementGate' }); $g | Select-Object -First 12 | ForEach-Object { Say "    $_" }
        if (($g -join "`n") -match 'GATE FAIL') { Fail "placement gate failed: a crown sits 1 m+ over a green or collar. Fix it, or add BoJa-approved lines to AutomationOutput\placement_exceptions.txt" }
        Say "  placement gate passed" 'Green'
    }
    Act "invoke TmpBuild.Build in C (all terrain colliders disabled, timeout $BuildTimeoutSec s)" {
        AssertGsproClosed
        $script:TBuild = Get-Date
        try { Bridge 'invoke' $BuildTimeoutSec @{ Tool = 'TmpBuild'; Method = 'Build' } | ForEach-Object { Say "    $_" } }
        catch { Say "  $($_.Exception.Message)" 'Red' }
        $script:BuildSecs = ((Get-Date) - $script:TBuild).TotalSeconds
        if (-not (Test-Path $BuildResult) -or (Get-Item $BuildResult).LastWriteTime -lt $script:TBuild) { Fail "no fresh $BuildResult; check C's Console" }
        $r = Get-Content $BuildResult -Raw; Say ($r -replace '(?m)^', '    ')
        if ($r -match 'FAILED' -or $r -notmatch 'Course Build Completed') { Fail "build failed (see above / C's Console / Assets\BuildLogs)" }
        if ($r -notmatch [regex]::Escape("save location $expectLoc")) { Fail "the build used another save location than $expectLoc" }
        Say ("  build finished in {0:N0} s" -f $script:BuildSecs) 'Green'
    }
    if (-not $BuildInPlace) {
        Act "put C's save location back to $origLoc and refresh C" {
            SetSaveLocation $origLoc
            try { Bridge 'refresh' 600 | Out-Null } catch { Say "  (refresh after restore failed; next Sync-EditorC restores it anyway)" 'Yellow' }
        }
    }
    Say "  Do NOT save editor C now (its terrain colliders are switched off in memory)." 'Yellow'
}
else { Say "  -SkipBuild: installing $StageCrse as is" 'Yellow' }

# ------------------------------------------------------------------ 4. install + verify
Step "4. Install and verify"
$tBuild = if ($script:TBuild) { $script:TBuild } else { $T0 }
$newFile = if ($BuildInPlace) { $Crse } else { $StageCrse }
Act "check the new build $newFile (exists, newer than the build start, size within 0.6x-${MaxGrowth}x of the old one)" {
    if (-not (Test-Path $newFile)) { Fail "no $newFile" }
    $n = Get-Item $newFile
    if (-not $SkipBuild -and $n.LastWriteTime -lt $tBuild) { Fail "$newFile is older than the build start ($($n.LastWriteTime))" }
    # hard size gate (Nov 1: a 2.16 GB build hung GSPro at "course decrypting"; GSPro decrypts into one array, max 2,147,483,647 bytes)
    if ($n.Length -gt 1950000000) { Fail ("new build is {0:N0} bytes, over the 1,950,000,000-byte limit (GSPro hangs at 'course decrypting' past 2 GiB); slim the build first" -f $n.Length) }
    $ratio = $n.Length / $old.Length
    if ($ratio -lt 0.6 -or $ratio -gt $MaxGrowth) { Fail ("new build is {0:N0} bytes = {1:N2}x the old one; looks wrong" -f $n.Length, $ratio) }
    Say ("  new build {0:N0} bytes ({1:N2}x old), written {2}" -f $n.Length, $ratio, $n.LastWriteTime) 'Green'
    $left = Get-ChildItem (Split-Path $newFile) -Force | Where-Object { $_.Name -like '*.manifest' -or $_.Extension -eq '.unity3d' -or ($_.Name -eq $Course -and -not $_.PSIsContainer) }
    if ($left) { Say ("  WARNING leftover bundle files: " + ($left.Name -join ', ') + " (CleanUpDirectory normally deletes them)") 'Yellow' }
}
if ($BuildOnly) { Say ("  -BuildOnly: staged build $StageCrse is {0:N0} bytes, SHA256 {1}; NOT installed" -f (Get-Item $StageCrse).Length, (Get-FileHash $StageCrse).Hash) 'Green'; exit 0 }
if (-not $BuildInPlace) {
    Act "copy $StageCrse -> $Crse (via a .partial file, then rename) and compare SHA256" {
        AssertGsproClosed
        $partial = "$Crse.partial"
        Copy-Item $StageCrse $partial -Force
        if ((Get-FileHash $partial).Hash -ne (Get-FileHash $StageCrse).Hash) { Remove-Item $partial; Fail "copy into the course folder is corrupt" }
        Move-Item $partial $Crse -Force
        Say "  installed, hash matches the staged build" 'Green'
    }
}
if ($InstallGkd) {
    Act "install GKD $InstallGkd -> $Gkd (JSON + SceneFolderName checked first)" {
        $j = Get-Content $InstallGkd -Raw | ConvertFrom-Json
        if ($j.SceneFolderName -ne $Course) { Fail "$InstallGkd has SceneFolderName '$($j.SceneFolderName)'" }
        Copy-Item $InstallGkd $Gkd -Force
        if ((Get-FileHash $InstallGkd).Hash -ne (Get-FileHash $Gkd).Hash) { Fail "GKD copy mismatch" }
        Say "  GKD installed (coursedetails.txt / .gspcrse.csv / scorecards are NOT regenerated: open + save in Greenkeeper if pars/yardages changed)" 'Yellow'
    }
}
Act "compare the course folder with the backup: only the .gspcrse$(if ($InstallGkd) { ' and GKD' }) may differ" {
    $now = FileTable $CourseDir
    foreach ($k in $script:Before.Keys) {
        if (-not $now.ContainsKey($k)) { Fail "$k disappeared from the course folder" }
        $changed = $now[$k].Hash -ne $script:Before[$k].Hash
        $allowed = ($k -eq "$Course.gspcrse") -or ($InstallGkd -and $k -eq "$Course.GKD")
        if ($changed -and -not $allowed) { Say "  WARNING $k changed" 'Yellow' }
        if ($k -eq "$Course.gspcrse" -and -not $changed) { Fail "the .gspcrse is unchanged (same hash as the backup)" }
    }
    foreach ($k in $now.Keys) { if (-not $script:Before.ContainsKey($k)) { Say "  WARNING new file in course folder: $k" 'Yellow' } }
    Say "  course folder OK: new .gspcrse, everything else as backed up" 'Green'
}
Act "read the bundle size line from Editor.log (GSP warns above 4 GB uncompressed)" {
    $log = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
    try {
        $fs = [IO.File]::Open($log, 'Open', 'Read', 'ReadWrite'); $sr = New-Object IO.StreamReader($fs); $txt = $sr.ReadToEnd(); $sr.Close()
        $m = [regex]::Matches($txt, "Level 0 '[^']*$Course[^']*' uses [\d\.]+ (GB|MB) compressed / [\d\.]+ (GB|MB) uncompressed\.")
        if ($m.Count) { Say "  $($m[$m.Count-1].Value)  (last one in Editor.log; Oct 25 build was 0.89 GB / 1.31 GB)" } else { Say "  no size line found (C may log elsewhere); check C's Console" 'Yellow' }
    } catch { Say "  couldn't read Editor.log: $($_.Exception.Message)" 'Yellow' }
}

# ------------------------------------------------------------------ 5. rollback
Step "5. Done"
if ($Go) {
    Say ("  total {0:N1} min. Start GSPro, load Tierra Rejada, check tees and pins." -f ((Get-Date) - $T0).TotalMinutes) 'Green'
    Show-Rollback
}
else {
    Say "  Dry run finished. Rollback would be:" 'Yellow'
    Say "  Copy-Item -Path '$BackupDir\course\*' -Destination '$CourseDir' -Recurse -Force" 'Yellow'
}
