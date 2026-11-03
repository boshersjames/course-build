# Course automation: Claude drives Unity on the PC

`unity_editor/CourseAutomation.cs` runs a course's editor tools in order, saves the scene, checks what was built,
renders screenshots from fixed camera spots and writes a summary. It works on any course: each course has a JSON
config (`automation/configs/<scene>.json`). The tools themselves are not changed.

## What it does

1. Opens the course scene and runs the steps in the config. Each step calls a tool method, the same as clicking its
   button (for example `TierraPlantShrubs.Plant`), or runs a menu item. Fields can be set first, such as CSV paths.
2. Checks that each step built something (`SHRUBS`, `DEEP_ROUGH`, detail layers on `Terrain (Inner)` ...). The tools
   show a dialog and stop when an input is missing, so without this check a step could fail without anyone noticing.
3. Saves the scene.
4. Inspects the objects in `inspect`: object, renderer and collider counts, missing materials, colliders without a
   physics material, layers, static flags. It also lists every root object in the scene. This should help find why
   fences and nets go missing in GSPro.
5. Renders screenshots: the fixed spots in `shots`, a frame around each named object (or each of its children), and a
   tee view plus a green view for every hole from the course's Greenkeeper GKD.
6. Writes `<project>\AutomationOutput\<date_time>\summary.md`, `summary.json`, `shots\*.png` and `index.html`.
   `AutomationOutput\latest.txt` points to the newest run.

## Install (PC, once)

1. `git fetch` and `git checkout claude/unity-automation` in `Documents\tierra-rejada` (or merge it).
2. Copy `unity_editor\CourseAutomation.cs` to `C:\GSP\GSPBaseProject_v4_2025_12_02\Assets\Editor\`.
3. Copy `automation\configs\TierraRejada.json` to `C:\GSP\GSPBaseProject_v4_2025_12_02\Assets\CourseAutomation\`.
   (`.\automation\Run-CourseAutomation.ps1 -SyncTools` does both copies.)
4. The Tierra steps also need the SoCal vegetation tools and `terrain_masks\` from the `claude/socal-vegetation` branch.

## Three ways to run it

**Batch mode, Unity closed** (the whole build, unattended):
```
cd $HOME\Documents\tierra-rejada
.\automation\Run-CourseAutomation.ps1                          # all enabled steps, then check and shots
.\automation\Run-CourseAutomation.ps1 -Steps "Plant shrubs,Paint grass"
.\automation\Run-CourseAutomation.ps1 -Steps none              # only check the scene and take screenshots
```
If PowerShell refuses to run scripts: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` once.

**Menu, Unity open:** Course Tools > Automation > Run pipeline / Screenshots only / Inspect scene.

**Live bridge, Unity open:** while the editor is open it reads command files from
`<project>\Automation\inbox` and writes the answers to `Automation\outbox`. This lets a Claude session on the PC
drive the open editor without restarting Unity:
```
.\automation\Send-UnityCommand.ps1 ping
.\automation\Send-UnityCommand.ps1 run                         # pipeline, using Assets\CourseAutomation\<scene>.json
.\automation\Send-UnityCommand.ps1 run -Steps "Fences, walls and nets"
.\automation\Send-UnityCommand.ps1 shots
.\automation\Send-UnityCommand.ps1 describe -Tool BuildFences  # its menu items, fields (current values) and methods
.\automation\Send-UnityCommand.ps1 invoke -Tool TierraImportDeepRough -Method Import
.\automation\Send-UnityCommand.ps1 menu -Path "GSP/Build Course"
.\automation\Send-UnityCommand.ps1 save
.\automation\Send-UnityCommand.ps1 refresh                     # after copying new .cs files in
```
Turn it off with Course Tools > Automation > Live bridge. A dialog box in the editor blocks the bridge until someone
clicks it. Batch mode has no dialogs.

## Config

```
{ "course": "...", "scene": "TierraRejada", "repo": "{Documents}/tierra-rejada", "saveScene": true,
  "steps": [ { "name": "Plant shrubs", "tool": "TierraPlantShrubs", "method": "Plant",
               "set": [ { "field": "csv", "value": "{Repo}/trees/tierra_shrubs_clean.csv" } ],
               "requireFiles": [ "{Repo}/trees/tierra_shrubs_clean.csv" ],
               "expectObject": "SHRUBS", "expectMinChildren": 1 },
             { "name": "Cart paths", "menu": "Tierra/Build Cart Paths" } ],
  "inspect": [ "SHRUBS" ],
  "shots": [ { "name": "range_net", "pos": [1120, 18, 860], "lookAt": [1120, 9, 922], "aboveGround": true },
             { "name": "fences", "frameObject": "FENCES", "eachChild": true } ],
  "gkd": "C:/GSProV1/Core/GSP/Courses/TierraRejada/TierraRejada.GKD", "teeType": "Blue" }
```
- Step options: `disabled` (keep it in the file but skip it), `optional` (a failure doesn't stop the run), `note` (ignored).
- `set` values: text, numbers, `true`/`false`, `x,y,z`, an asset path (`Assets/...`), `scene:Object Name`,
  `resources:Physics/GSPleaves`, `;` between items for arrays.
- Tokens: `{Documents}`, `{Repo}`, `{Project}`, `{Scene}`, `{Course}`.
- Shot positions are Unity metres; with `aboveGround` the y values are heights above the terrain.
- For Moorpark: copy the config, rename it to the Moorpark scene name, change the scene, repo, GKD and shot spots.

## Tierra config notes

- Fence, wall, net, cart path and LiDAR tree steps use the real method names found on the PC (Oct 25 first run).
- GSP > Build Course is not automated yet. `menu -Path "GSP/Build Course"` opens its window, and `describe` on its class
  shows how to call it directly.
