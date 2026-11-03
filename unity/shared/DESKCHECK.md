# Desk check of the cloud-written editor scripts (2026-11-03)

No C# compiler exists in the cloud container, so `CourseGreenChecks.cs` and `ApplyTintJson.cs` were read line by line against
the 164 scripts that compile today in the PC's `Assets\Editor` (snapshot: tierra-rejada `unity_editor_pc/`).

## Target language level
The live scripts use string interpolation (`$"..."`, 5 files), `?.` (1 file) and `string.Join(string, List<string>)`, so the
project runs Unity 2018.2's .NET 4.x runtime with C# 6/7 (Roslyn). No `out var`, tuples or pattern matching appear anywhere, so
the new scripts avoid them too; they would compile either way.

## Checked
- APIs: `Vector2Int` as a dictionary key, `LODGroup`, `Renderer.bounds` / `Encapsulate` / `Intersects`, `Physics.RaycastAll`,
  `Collider.Raycast(Ray, out RaycastHit, float)`, `Collider.sharedMaterial`, `EditorWindow.GetWindow<T>(string)`, `MenuItem`,
  `EditorUtility.OpenFilePanel`, `AssetDatabase.FindAssets("name t:Material")`, `SceneManager.GetActiveScene().GetRootGameObjects()`,
  `JsonUtility.FromJson<T>` with nested `[Serializable]` classes, `float[]` and `bool` fields and default values. All exist in 2018.2.
- JsonUtility limits: no dictionaries, no top-level arrays, unknown keys ignored, public fields only. The rule subset
  (`GreensRules`), `CourseJson` and the GKD subset (`G`/`Hole`/`Tee`/`Pin`/`P`) respect that. `rules/greens.json` has a `$schema`
  string key; it is ignored.
- `string.Join` is always given a `string[]` (`.ToArray()` or `new[] {}`), `Count(predicate)` has `System.Linq`, `Regex` and
  `CultureInfo` are imported, every `ToString("F1", CI)` is a float/double overload, `new Color(r,g,b,a)` arity is right.
- No `Object` ambiguity: `ApplyTintJson` does not import `System`; `CourseGreenChecks` imports `System` but never names `Object`.
- Brace, bracket and parenthesis balance: 0 / 0 / 0 in both files.
- Variable scoping traced through the dense one-liners; every name is declared before use in an enclosing scope.
- Tiers: the live scripts compare physic material names exactly (`col.sharedMaterial.name == "GSPgreen"`, `"GSPfairway"`,
  `Resources.Load<PhysicMaterial>("Physics/GSPrough")`), so `Contains("GSPgreen")` etc. match the real names.

## Changed
Four `foreach (var x in list.Where(x => ...))` patterns had the loop variable and the lambda parameter sharing a name, which the
pre-C# 8 compiler rejects (CS0136). Renamed the lambda parameters at the hole loop, the collider loop, the scene-root loop and the
report loop; each carries a `// desk-check:` comment. No behaviour change.

## Still unverified until a real compile
- Implicit bool on `PhysicMaterial` (`h.collider.sharedMaterial ? ... : ""`): `PhysicMaterial` derives from `UnityEngine.Object`, so
  it should work; if the compiler complains, write `!= null`.
- `EditorGUILayout.TextArea` growing with a long report; cosmetic.
- Runtime: `GameObject.Find("OPCD Meshes")` and the `_Green_-_Mesh` name pattern match the Tierra scene; Moorpark's scene names
  should match since the same OPCD importer made them, but check the first run's row count.

## First run on the PC
1. From the course folder: `..\course-build\automation\Run-CourseAutomation.ps1 -SyncTools -Steps none` (copies the scripts,
   `course.json` with `course_repo_abs`, and `rules/*.json` into the project; Unity closed) or copy by hand and `refresh`.
2. Unity open: `..\course-build\automation\Send-UnityCommand.ps1 refresh`, then `ping` until it answers.
3. `..\course-build\automation\Send-UnityCommand.ps1 invoke -Tool CourseGreenChecks -Method RunAll`
   or the menu `Course Checks > Greens: all-18 report`.
4. Output: `<project>\AutomationOutput\checks\greens_all18.md`, `.csv`, `edge_survey.csv`, `pin_area.csv`, `grid_heights_h*.csv`.
   Then `python ..\course-build\tools\gate_report.py` reads them.
5. Tint test: write `Assets\CourseLook\tint.json` from `colour_match.py --json`, menu `Course Look > Apply Tint JSON`.
Any compile message: fix in `Assets\Editor`, copy the file back to `course-build\unity\shared\`, commit.
