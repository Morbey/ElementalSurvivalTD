# Unity CLI notes

What the team has learned about driving this project with the `unity` CLI. Add a line when you hit a new limit; remove one when it stops being true.

## Editor open or closed
- The team works in a git worktree with its own `Library` and never touches the editor on Eduardo's checkout. Every `unity` call names the worktree by its full path, never by project name (`unity test <worktree> …`, `unity command --project-path <worktree> …`), and `unity status --project-path <worktree>` comes before any `unity command`.
- `unity command …` needs the interactive editor open: `unity projects open <worktree> --until-ready`. Close it with `unity command --project-path <worktree> editor quit --save true`, or `unity close <worktree> --force` if it is stuck.
- `unity test` and `unity build` start their own batch editor and refuse to run while the interactive editor has the project open.
- Do not run tests inside the open editor (`unity command test run`): it hung on a save-scene prompt and blocked every later command.

## Scenes, assets and settings
- After writing files to disk, run `unity command script eval --code 'UnityEditor.AssetDatabase.Refresh()'`; the editor then creates the `.meta` files.
- `component set_field` handles numbers, object references (`{"path": "Assets/…"}`, `{"hierarchyPath": "…"}`) and `Vector3`, but not `Vector2Int`. Use `script eval` with `SerializedObject` for those, then `AssetDatabase.SaveAssets()`.
- `scene add_to_build` and `remove_from_build` change the list only in memory. Save it explicitly and check `ProjectSettings/EditorBuildSettings.asset` on disk.
- `settings.player set` does not cover the WebGL player settings. Set them through `script eval` (`PlayerSettings.WebGL.…`), save, and check `ProjectSettings/ProjectSettings.asset`.
- `asset copy` refuses a source outside `Assets/`. Use `AssetDatabase.CopyAsset` through `script eval`.
- For multi-step asset or scene work, write a C# script outside `Assets/` and run `unity command script eval --file <path>`. In it, do not use `??` on `GetComponent` or `GameObject.Find` results: Unity's fake null defeats it.
- Smoke-check a scene with `unity command editor.playmode start`, then `script eval` calls on the live objects, then `editor.playmode stop`; Play mode changes are discarded.
- In a `script eval`, `EditorSceneManager.OpenScene` destroys objects loaded or created before it. Open the scene first, then load or create assets.
- In Play mode, send a real click with `InputSystem.QueueStateEvent(Mouse.current, …)` over a few frames from `EditorApplication.update`; UI Toolkit receives it with no `EventSystem`. `Mouse.current` read from that callback does not show the queued state.
- Opening the editor on a worktree rewrites `.vscode/settings.json`. Revert it before the commit.
- A malformed `unity command` writes "No command named …" errors to the editor console. Clear it (`unity command console clear`) before a clean-compile check.

## Builds
- WebGL is not a zero-code target for `unity build --target`. It builds through the profile `Assets/Settings/Build Profiles/WebGL.asset`: `tools/webgl.sh build`, then `tools/webgl.sh serve` for `http://localhost:8765`. Set `WEBGL_PORT` to serve on another port; `serve` refuses a port that answers from a server this project did not start, and `stop` ends only the server it started on that port.
- A server started before `WEBGL_PORT` existed keeps its PID in `Logs/webgl-serve.pid`, which the script no longer reads: kill it by hand once, then delete this line.
- `build create_profile` copies the player settings into the profile. That override was removed, so the profile uses the global scene list and `ProjectSettings.asset`.
- `unity build` refuses uncommitted changes unless it gets `--allow-dirty-build`.
- The first build after a change of platform takes about two minutes; an unchanged rebuild is incremental and takes seconds.

## Tests
- `unity test <project> --mode EditMode|PlayMode --report-format junit --output <path outside Assets/>`.
- An asmdef with no scripts is not compiled and Unity warns about it.
