# Sends one command to the open Unity editor (CourseAutomation live bridge) and prints the answer.
#   .\automation\Send-UnityCommand.ps1 ping
#   .\automation\Send-UnityCommand.ps1 run                                # whole pipeline from the scene's config
#   .\automation\Send-UnityCommand.ps1 run -Steps "Plant shrubs"
#   .\automation\Send-UnityCommand.ps1 shots
#   .\automation\Send-UnityCommand.ps1 describe -Tool BuildFences
#   .\automation\Send-UnityCommand.ps1 invoke -Tool TierraPlantShrubs -Method Plant
#   .\automation\Send-UnityCommand.ps1 invoke -Tool TierraImportDeepRough -Method Import -Set @{ folder = "C:\Users\user\Documents\tierra-rejada\deep_rough\areas" }
#   .\automation\Send-UnityCommand.ps1 menu -Path "GSP/Build Course"
#   .\automation\Send-UnityCommand.ps1 save | refresh | openScene -Scene TierraRejada
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Cmd,
    [string]$Project = "C:\GSP\GSPBaseProject_v4_2025_12_02",
    [string]$Config = "",
    [string]$Steps = "",
    [switch]$NoShots,
    [string]$Tool = "",
    [string]$Method = "",
    [hashtable]$Set = @{},
    [string]$Path = "",
    [string]$Scene = "",
    [int]$TimeoutSec = 1800
)
$ErrorActionPreference = "Stop"
$inbox = Join-Path $Project "Automation\inbox"
$outbox = Join-Path $Project "Automation\outbox"
New-Item -ItemType Directory -Force $inbox, $outbox | Out-Null
$id = (Get-Date -Format "yyyyMMdd_HHmmss_fff")
$sets = @($Set.GetEnumerator() | ForEach-Object { @{ field = $_.Key; value = [string]$_.Value } })
$body = [ordered]@{ id = $id; cmd = $Cmd; config = $Config; steps = $Steps; shots = (-not $NoShots); tool = $Tool; method = $Method; path = $Path; scene = $Scene; set = $sets }
$tmp = Join-Path $inbox "$id.tmp"
$body | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $tmp
Move-Item $tmp (Join-Path $inbox "$id.json")

$answer = Join-Path $outbox "$id.json"
$t0 = Get-Date
while (-not (Test-Path $answer)) {
    if (((Get-Date) - $t0).TotalSeconds -gt $TimeoutSec) {
        Write-Host "No answer after $TimeoutSec s. Is Unity open with the project, not compiling, and no dialog box waiting?"
        exit 1
    }
    Start-Sleep -Milliseconds 500
}
Start-Sleep -Milliseconds 200
$a = Get-Content $answer -Raw | ConvertFrom-Json
Write-Host "[$($a.status)] $($a.cmd)  scene $($a.scene)"
Write-Host $a.message
if ($a.outputDir) { Write-Host "Output: $($a.outputDir)" }
if ($a.status -ne "ok") { exit 2 }
