# Unity CLI notes

What the team has learned about driving this project with the `unity` CLI. Add a line when you hit a new limit; remove one when it stops being true.

## Editor open or closed
- `unity command …` needs the interactive editor open: `unity projects open . --until-ready`. Close it with `unity command editor quit --save true`, or `unity close . --force` if it is stuck.
- `unity test` and `unity build` start their own batch editor and refuse to run while the interactive editor has the project open.
- Do not run tests inside the open editor (`unity command test run`): it hung on a save-scene prompt and blocked every later command.

## Scenes, assets and settings
- After writing files to disk, run `unity command script eval --code 'UnityEditor.AssetDatabase.Refresh()'`; the editor then creates the `.meta` files.
- `component set_field` handles numbers, object references (`{"path": "Assets/…"}`, `{"hierarchyPath": "…"}`) and `Vector3`, but not `Vector2Int`. Use `script eval` with `SerializedObject` for those, then `AssetDatabase.SaveAssets()`.
- `scene add_to_build` and `remove_from_build` change the list only in memory. Save it explicitly and check `ProjectSettings/EditorBuildSettings.asset` on disk.
- `settings.player set` does not cover the WebGL player settings. Set them through `script eval` (`PlayerSettings.WebGL.…`), save, and check `ProjectSettings/ProjectSettings.asset`.
- `asset copy` refuses a source outside `Assets/`. Use `AssetDatabase.CopyAsset` through `script eval`.
- A malformed `unity command` writes "No command named …" errors to the editor console. Clear it (`unity command console clear`) before a clean-compile check.

## Builds
- WebGL is not a zero-code target for `unity build --target`. It builds through the profile `Assets/Settings/Build Profiles/WebGL.asset`: `tools/webgl.sh build`, then `tools/webgl.sh serve` for `http://localhost:8765`.
- `build create_profile` copies the player settings into the profile. That override was removed, so the profile uses the global scene list and `ProjectSettings.asset`.
- `unity build` refuses uncommitted changes unless it gets `--allow-dirty-build`.
- The first build after a change of platform takes about two minutes; an unchanged rebuild is incremental and takes seconds.

## Tests
- `unity test <project> --mode EditMode|PlayMode --report-format junit --output <path outside Assets/>`.
- An asmdef with no scripts is not compiled and Unity warns about it.
