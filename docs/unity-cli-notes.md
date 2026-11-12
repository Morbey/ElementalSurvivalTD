# Unity CLI notes

What the team has learned about driving this project with the `unity` CLI. Add a line when you hit a new limit; remove one when it stops being true.

## Editor open or closed
- The team works in a git worktree with its own `Library` and never touches the editor on Eduardo's checkout. Every `unity` call names the worktree by its full path, never by project name (`unity test <worktree> …`, `unity command --project-path <worktree> …`), and `unity status --project-path <worktree>` comes before any `unity command`.
- `unity command …` needs the interactive editor open: `unity projects open <worktree> --until-ready`. Close it with `unity command --project-path <worktree> editor quit --save true`, or `unity close <worktree> --force` if it is stuck.
- An editor opened with `--until-ready` once quit cleanly seconds after reporting ready, with no error in `Logs/Editor.log`. Check `unity status` before the first command and open it again if the list is empty.
- `unity test` and `unity build` start their own batch editor and refuse to run while the interactive editor has the project open.
- Only one editor, batch or interactive, may use a checkout at a time. Hold the lock through `tools/unity-lock.sh`: `run <command>` around a `unity test` or build, `take` and `release` around an interactive session. Never hand-write a wait loop: one with a wrong path waited for ever.
- A reviewer who compiles Core with the .NET SDK bundled in Unity sets `DOTNET_CLI_HOME` and `NUGET_PACKAGES` to a scratch folder and `DOTNET_CLI_TELEMETRY_OPTOUT=1`, so nothing is written to the home directory.
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
- In a `script eval`, extension methods do not resolve: call `UnityEngine.UIElements.UQueryExtensions.Q<Label>(root, name)`, not `root.Q<Label>(name)`.
- Saving a scene after a script rename keeps the old `m_EditorClassIdentifier`. Set it through `SerializedObject.FindProperty("m_EditorClassIdentifier")`, then save.
- In Play mode, send a real click with `InputSystem.QueueStateEvent(Mouse.current, …)` over a few frames from `EditorApplication.update`; UI Toolkit receives it with no `EventSystem`. `Mouse.current` read from that callback does not show the queued state.
- Opening the editor on a worktree rewrites `.vscode/settings.json`. Revert it before the commit.
- On a URP material, `renderQueue` set from a script is reset to the shader's queue plus `_QueueOffset`. Set `_QueueOffset` (for example 1 for 3001), then the queue.
- Another agent saving a script in the worktree during Play reloads the domain, and every view then throws a `NullReferenceException` each frame. Call `AssetDatabase.DisallowAutoRefresh()` before Play and `AllowAutoRefresh()` after Stop.
- `capture game_view --output-path <png>` works in Play and includes the UI Toolkit HUD. Queued `KeyboardState` and `MouseState` events, each sent from its own `script eval` (press, then release), reach both the game and UI Toolkit.
- A malformed `unity command` writes "No command named …" errors to the editor console. Clear it (`unity command console clear`) before a clean-compile check.
- Set the Game view to the canvas size with `PlayModeWindow.SetCustomRenderingResolution(960, 540, "<name>")` in a `script eval`. `capture game_view` then still returns 1280 x 720; `ScreenCapture.CaptureScreenshot(<absolute png>)` from a `script eval` in Play saves the real size with the HUD.
- The turn between two CLI calls is longer than a wave countdown. Freeze the battle with `Time.timeScale = 0` while setting up (frames, input and UI still run), then set it back to 1.
- To observe every drawn frame, add a callback to `RenderPipelineManager.endCameraRendering` filtered on `Camera.main` and `Time.frameCount`; it runs after `LateUpdate` and the UI update, so resolved styles are the frame's. Write the log to a file and remove the callback when done.
- The `Hud` object carries only a `UIDocument`. A `PanelRenderer` on it with the same UXML draws a second, unwired copy of the HUD over the real one (found in 4c; it was invisible while the bars had no background). Dump `panel.visualTree` when the HUD draws but shows nothing.

## Builds
- WebGL is not a zero-code target for `unity build --target`. It builds through the profile `Assets/Settings/Build Profiles/WebGL.asset`: `tools/webgl.sh build`, then `tools/webgl.sh serve` for `http://localhost:8765`. Set `WEBGL_PORT` to serve on another port; `serve` refuses a port that answers from a server this project did not start, and `stop` ends only the server it started on that port.
- A server started before `WEBGL_PORT` existed keeps its PID in `Logs/webgl-serve.pid`, which the script no longer reads: kill it by hand once, then delete this line.
- `build create_profile` copies the player settings into the profile. That override was removed, so the profile uses the global scene list and `ProjectSettings.asset`.
- `unity build` refuses uncommitted changes unless it gets `--allow-dirty-build`.
- The first build after a change of platform takes about two minutes; an unchanged rebuild is incremental and takes seconds.

## Tests
- `unity test <project> --mode EditMode|PlayMode --report-format junit --output <path outside Assets/>`.
- When a test fails, `unity test` exits 8 and prints only "Unity process exited with code 2"; the junit report is still written, so read the failures there. Compiler warnings are in the worktree's `Logs/Editor.log`.
- An asmdef with no scripts is not compiled and Unity warns about it.
- `unity test … --filter <test name> --retries <n>` re-runs one test, to tell a flaky failure from a real one without the whole suite.
- A batch `unity test` empties `Library/LastSceneManagerSetup.txt`, so the next interactive editor on that checkout opens an untitled scene and Play shows nothing. Open `Assets/_Game/Scenes/Prototype.unity`.
