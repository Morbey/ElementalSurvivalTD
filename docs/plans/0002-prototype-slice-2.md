# Technical plan 0002 – Prototype slice 2, steps 2a and 2b

2a: a WebGL build of slice 1 that runs in the browser. 2b: the player builds the maze with blocking towers. Step 2c gets its own plan. Architecture: `docs/adr/0001-folder-structure-asmdefs-layers.md` and `docs/adr/0002-presentation-layer-ui-toolkit.md`.

**2a and 2b are accepted separately.** Every 2a task, its tests and the browser check finish before any 2b task starts. No 2a task depends on 2b work.

## Approach
**2a.** The first WebGL build is the project's first IL2CPP build with engine-code stripping, and a player has no URP default material to hand to `GameObject.CreatePrimitive`. So the bootstrap stops creating primitives and cloning materials: it instantiates a cell prefab and an enemy prefab and picks among material assets, all under `Assets/_Game/Presentation/`. Nothing else moves; there is no new assembly in 2a. The build uses the CLI's built-in `unity build`, wrapped with a local static server in one script. Compression is Gzip with the decompression fallback, which works on any static server. A custom Editor build method was discarded as unneeded; it stays as the named fallback in T2a-3.

**2b.** A new Core class, `Board`, owns the map, the flow field, the spawn, the exit, the tower cells and the living followers, and holds the placement rule. To place a tower it blocks the cell, recomputes the field and checks that the spawn and every follower's `To` are still reachable; if not, it unblocks and recomputes. That is at most two breadth-first searches over 144 cells per click, with no allocation. A second scratch field was discarded: it only pays off for a per-hover validity preview, which is in the Parking lot.

A follower can no longer stall, for four reasons. Its `To` cell is never blocked, because placement is refused on any follower's `From` or `To`. Its `To` cell stays connected, by the check above. The check and the commit are one synchronous call, so no follower steps on a rejected field. And a follower reads the field only on reaching a cell centre, when the field is already the new one. Removal only opens cells, so it recomputes without a check.

Input lives in Gameplay: it polls `Mouse.current`, casts a ray against the y = 0 plane and converts with `MapDefinition.WorldToCell`. Its click and hover handlers are public methods that take a screen position, so the PlayMode tests drive the production path without simulating a device. The Presentation assembly (ADR 0002) takes the grid tiles and the camera framing from the bootstrap and adds the tower visuals, the hover highlight and the refusal message in UI Toolkit. The enemy respawns by restarting its one follower; nothing is instantiated or destroyed, so pooling is still not needed.

## Editor sequencing
"Open" means asset, scene or settings work through `unity command`. "Closed" means a batch build or a test run. Each step opens the editor once and closes it once.

| Order | Work | Editor |
|---|---|---|
| 1 | T2a-1, T2a-2 | Open. Save everything, then close. |
| 2 | T2a-3, 2a tests, serve, browser check, acceptance of 2a | Closed |
| 3 | T2b-1 to T2b-4 | Open. Save everything, then close. |
| 4 | T2b-5, 2b tests, serve, browser check, acceptance of 2b | Closed |

## Step 2a – WebGL build of slice 1

### Acceptance criteria
- **2a-AC1** – With the editor closed, `tools/webgl.sh build` writes a non-development WebGL player to `Builds/WebGL/` and exits with 0.
- **2a-AC2** – `tools/webgl.sh serve` leaves the build reachable at `http://localhost:8765`. In desktop Chrome and Safari the page loads and runs with no error in the browser console.
- **2a-AC3** – The browser shows the same scene, colours, layout and motion as the editor: four distinct cell kinds, the enemy on the S route, despawn at the exit. Nothing is pink or missing. The softer picture of the WebGL quality level is accepted.
- **2a-AC4** – No visual is built from code: `Assets/_Game/` outside `Tests/` contains no `CreatePrimitive` and no `new Material`, and every renderer in the running scene shares a material asset from `Assets/_Game/Presentation/Materials/`.
- **2a-AC5** – Slice 1 still holds in the editor: its EditMode and PlayMode tests pass unchanged.

### T2a-1 – Visuals as assets
Serves 2a-AC3, 2a-AC4 and 2a-AC5. Editor open.

Create through `unity command`, each with its `.meta`:
- `Assets/_Game/Presentation/Materials/`: `Cell_Free`, `Cell_Blocked`, `Cell_Spawn`, `Cell_Exit` and `Enemy`, all URP Lit, with the colours the `Bootstrap` object in the scene holds today.
- `Assets/_Game/Presentation/Prefabs/Cell.prefab`: an empty root with a child `Tile` (cube mesh, scale 0.95 x 0.1 x 0.95, local y -0.05, material `Cell_Free`, no collider).
- `Assets/_Game/Presentation/Prefabs/Enemy.prefab`: a root with `EnemyAgent` and a child `Body` (capsule mesh, scale 0.5 x 0.25 x 0.5, local y 0.25, material `Enemy`, no collider).

Both prefabs are authored for a cell size of 1; the bootstrap scales the root by `map.CellSize`.

Change `Assets/_Game/Gameplay/PrototypeBootstrap.cs`:
- Add serialized references: the cell prefab, the blocked, spawn and exit materials, and the enemy prefab (typed `EnemyAgent`).
- Instantiate the prefabs instead of creating primitives, and swap the tile's `sharedMaterial` for the three non-free kinds.
- Remove `CreateVisual`, `Tinted`, the five colour fields, `cellGap`, `cellThickness` and `enemyScale`.
- Keep the root object named `Grid` and the enemy on the spawn cell centre; the slice 1 tests rely on both.

Assign the new references on the `Bootstrap` object in `Prototype.unity` and save the scene.

**Done when:** clean compile; Play in the editor looks as it did before; `grep -rnE "CreatePrimitive|new Material" Assets/_Game --include=*.cs` matches nothing outside `Tests/`.

### T2a-2 – WebGL settings and the build script
Serves 2a-AC1 and 2a-AC2. Editor open for the settings; runs after T2a-1 in the same session.

- Through the editor API, set the WebGL player to Gzip compression with the decompression fallback on (`PlayerSettings.WebGL.compressionFormat`, `PlayerSettings.WebGL.decompressionFallback`). Change no other setting, including the quality levels.
- Create `tools/webgl.sh`, executable, resolving the project root from its own location:
  - `build` runs `unity build <root> --target WebGL --output-path <root>/Builds/WebGL --allow-dirty-build` and returns its exit code. The flag is needed because builds are tested before the orchestrator commits.
  - `serve` starts `python3 -m http.server 8765 --bind 127.0.0.1 --directory <root>/Builds/WebGL` detached, waits until `http://localhost:8765/` answers, prints the URL and returns. If the port already answers it only prints the URL.
  - `stop` ends that server.
  - With no argument it runs `build`, then `serve`.

**Done when:** the settings are saved in `ProjectSettings/ProjectSettings.asset`, the script exists, and the editor is saved and closed.

### T2a-3 – First build
Serves 2a-AC1, 2a-AC2 and 2a-AC3. Editor closed. Runs after T2a-2.

Run `tools/webgl.sh`. The first build compiles IL2CPP for the first time and may take several minutes. Fix whatever the build log reports.

**Fallback, only if the built-in build cannot produce the player:** add `Assets/_Game/Editor/ElementalTD.Editor.asmdef` (Editor platform only) with one static method that calls `BuildPipeline.BuildPlayer` for the scenes in Build Settings, pass it with `--execute-method`, and report the deviation so that ADR 0001's folder table gets the row.

**Done when:** the script exits with 0; `Builds/WebGL/index.html` and the loader, framework, data and wasm files under `Builds/WebGL/Build/` exist; `curl -sI http://localhost:8765/` returns 200.

### Tests for the qa-tester (2a)
Editor closed.
- **2a-AC5:** run the existing EditMode and PlayMode suites unchanged.
- **2a-AC4, EditMode:** scan the `.cs` files under `Assets/_Game/` outside `Tests/` and assert that none contains `CreatePrimitive` or `new Material`.
- **2a-AC4, PlayMode:** in the loaded `Prototype` scene, every `Renderer` has a `sharedMaterial` whose asset path starts with `Assets/_Game/Presentation/Materials/` and whose shader name starts with `Universal Render Pipeline/`. The tile at the spawn, the exit, a wall and a free cell use four different materials.
- **2a-AC1:** `tools/webgl.sh build` exits with 0 and the files listed in T2a-3 exist.
- **2a-AC2, server part:** after `tools/webgl.sh serve`, `curl` returns 200 for `index.html` and for every file in `Builds/WebGL/Build/`. Leave the server running.
- **Orchestrator, then Eduardo (2a-AC2 and 2a-AC3):** open `http://localhost:8765` in Chrome and Safari, compare with the editor, and check the browser console.

## Step 2b – The player builds the maze

### Acceptance criteria
- **2b-AC1** – A left click on a free cell places a tower there: a tower visual appears, the cell blocks, and the route is recalculated in the same frame, so the enemy walks around it.
- **2b-AC2** – A left click on a tower removes it, and the route is recalculated in the same frame. Fixed walls cannot be removed.
- **2b-AC3** – A placement is refused, and nothing changes, when the cell is, in this order of priority: the spawn or the exit; a fixed wall; the `From` or `To` cell of a living enemy; a cell whose blocking would cut the spawn or any living enemy off from the exit.
- **2b-AC4** – Each refusal shows its own line of text on screen: "Cannot build on the spawn or the exit.", "Cannot build on a wall.", "An enemy is in the way." and "That would block the path." The line hides after 2 seconds of unscaled time; a new refusal replaces it and restarts the timer. The texts and the duration are serialized fields. A click outside the grid does nothing.
- **2b-AC5** – Whatever sequence of placements and removals the player makes, the enemy never stops before the exit and never enters a blocked cell.
- **2b-AC6** – When the enemy reaches the exit it restarts at the spawn and walks the current route again. There is always exactly one enemy, and none is instantiated or destroyed after start-up.
- **2b-AC7** – The grid cell under the mouse shows a highlight in one neutral colour, on free cells and on tower cells alike. There is no highlight when the mouse is off the grid.
- **2b-AC8** – When the aspect of the view changes, the camera re-frames so that the whole grid stays in view.
- **2b-AC9** – The placement rule lives in `ElementalTD.Core`, which still compiles with `noEngineReferences: true`. `ElementalTD.Presentation` exists, and Gameplay does not reference it.
- **2b-AC10** – After warm-up, `Board.TryPlaceTower`, `Board.TryRemoveTower`, `PathFollower.Step` and `PathFollower.Restart` allocate 0 bytes. The per-frame code for input, hover, the message timer and the camera has no `new` of a reference type, LINQ, closure, boxing or string building.
- **2b-AC11** – All of the above works with the mouse in the WebGL build at `http://localhost:8765`, in Chrome and Safari.

### T2b-1 – Core: the board and its rule
Serves 2b-AC1, 2b-AC2, 2b-AC3, 2b-AC5, 2b-AC6, 2b-AC9 and 2b-AC10. Code only; in parallel with T2b-2.

In `Assets/_Game/Core/`:
- `GridMap.cs`: add `internal void SetBlocked(GridPos cell, bool blocked)`. Only `Board` changes the map.
- `PathFollower.cs`: add `void Restart(GridPos start)`, which leaves the follower in the state the constructor gives; the constructor calls it.
- `PlacementResult.cs`: `enum PlacementResult { Placed, OutOfBounds, SpawnOrExit, CellBlocked, EnemyInTheWay, WouldBlockPath }`. `CellBlocked` covers a fixed wall and an existing tower.
- `Board.cs`:

```csharp
public sealed class Board
{
    public Board(GridMap map, GridPos spawn, GridPos exit); // computes the field; throws if spawn or exit is out of bounds
    public GridMap Map { get; }
    public FlowField Field { get; }
    public GridPos Spawn { get; }
    public GridPos Exit { get; }
    public bool HasTower(GridPos cell);          // false when out of bounds
    public PathFollower AddFollower();           // starts at Spawn and counts for the placement rule
    public PlacementResult TryPlaceTower(GridPos cell);
    public bool TryRemoveTower(GridPos cell);    // false when the cell has no tower
}
```

`TryPlaceTower` tests in the order of the enum, then blocks, recomputes and checks `Spawn` and every follower's `To`, and reverts on failure, as the Approach describes. Towers are one `bool[]`; followers are one list, read by index.

**Done when:** Core compiles with no `UnityEngine`; there is no allocation in `TryPlaceTower`, `TryRemoveTower` or `Restart`.

### T2b-2 – Data and assets
Serves 2b-AC1, 2b-AC4 and 2b-AC7. Editor open; in parallel with T2b-1.

- `Assets/_Game/Data/MapDefinition.cs`: add `GridPos WorldToCell(Vector3 world)` next to `CellToWorld`, flooring x and z by `cellSize`. The result may be out of bounds.
- Through `unity command`:
  - `Presentation/Materials/Tower` (URP Lit) and `Presentation/Materials/Hover` (URP Unlit, transparent, a neutral light colour).
  - `Presentation/Prefabs/Tower.prefab`: a root with a child cube of about 0.7 of a cell, resting on the grid, with no collider.
  - `Presentation/Prefabs/Hover.prefab`: a root with a flat, cell-sized child placed above the tower's top, so that it shows on tower cells too.
  - `Presentation/UI/PanelSettings.asset`, with a runtime theme assigned and scaling with the screen size from a 960 x 600 reference.
- Written as text: `Presentation/UI/Hud.uxml`, with one `Label` named `placement-message`, and `Presentation/UI/Hud.uss`, which puts it top-centre and makes it legible on the grid.

**Done when:** clean compile and the assets exist with their `.meta` files.

### T2b-3 – Gameplay: board ownership, input and respawn
Serves 2b-AC1, 2b-AC2, 2b-AC3, 2b-AC6, 2b-AC7 and 2b-AC10. Code only; runs after T2b-1 and T2b-2.

- `ElementalTD.Gameplay.asmdef`: add the `Unity.InputSystem` reference.
- `PrototypeBootstrap.cs`:
  - Build the `Board` in `Awake` and expose it and the map as read-only properties.
  - Spawn the enemy in `Start`, as today, unless the spawn cannot reach the exit.
  - Remove the grid visuals, the camera framing and their serialized fields; T2b-4 takes them over.
- `EnemyAgent.cs`: `Init(Board board, float speedCellsPerSecond, MapDefinition map)` takes its follower from `board.AddFollower()`. On arrival it calls `Restart(board.Spawn)` instead of destroying the object. This loop is the prototype's stand-in for waves and goes away in slice 3.
- New `TowerPlacementInput.cs`, with serialized references to the bootstrap and the camera:

```csharp
public event Action<GridPos> TowerPlaced;
public event Action<GridPos> TowerRemoved;
public event Action<PlacementResult> PlacementRefused; // never raised for OutOfBounds
public bool HasHoverCell { get; }
public GridPos HoverCell { get; }
public void Hover(Vector2 screenPosition);
public void Click(Vector2 screenPosition); // removes when the cell has a tower, otherwise tries to place
```

`Update` only reads `Mouse.current` (returning when it is null), calls `Hover` with the pointer position and calls `Click` when the left button was pressed this frame. Picking is `Plane.Raycast` on the y = 0 plane with the camera's ray, then `WorldToCell`.

**Done when:** clean compile; no rule in any MonoBehaviour beyond the remove-or-place choice and the restart call; no allocation source in `Update`, `Hover` or `Click`.

### T2b-4 – Presentation: views, message and scene wiring
Serves 2b-AC1, 2b-AC2, 2b-AC4, 2b-AC7, 2b-AC8 and 2b-AC9. Editor open; runs after T2b-3.

Create `Assets/_Game/Presentation/ElementalTD.Presentation.asmdef`, referencing Core, Data and Gameplay, and three components that read the board in `Start`:
- `BoardView.cs`:
  - Builds the tiles under a root named `Grid` (the code moved from the bootstrap).
  - On `TowerPlaced` it instantiates the tower prefab as `Tower x,y` under a root named `Towers`; on `TowerRemoved` it destroys it.
  - Keeps one hover instance named `Hover`. In `LateUpdate` it shows it on `HoverCell`, or hides it when `HasHoverCell` is false.
- `BoardCameraFramer.cs`: the framing code moved from the bootstrap, run in `Start` and again whenever the camera's aspect differs from the last one it framed for.
- `PlacementMessageView.cs`:
  - Serialized: the `UIDocument`, one string per refusal reason and the duration.
  - On `PlacementRefused` it sets the label's text and shows it; it hides it when `Time.unscaledTime` passes the deadline.
  - The label starts hidden and ignores picking.

In `Prototype.unity`, through `unity command`: add `TowerPlacementInput`, the three views and a `UIDocument` using `Hud.uxml` and the panel settings; assign every reference, with the four texts of 2b-AC4 and a duration of 2; save the scene.

**Done when:** clean compile with an empty console; in the editor a click places and removes towers, the enemy re-routes and loops, each refusal shows its line, and the hover follows the mouse; the editor is saved and closed.

### T2b-5 – WebGL rebuild
Serves 2b-AC11. Editor closed. Runs after T2b-4.

Run `tools/webgl.sh`. **Done when:** it exits with 0 and `http://localhost:8765` serves the new build.

### Tests for the qa-tester (2b)
Editor closed. Add the Presentation reference to the PlayMode test asmdef. Slice 1's AC4 (despawn at the exit) is replaced by 2b-AC6, so rewrite its PlayMode test.

**EditMode**, on `Board`:
- **2b-AC1, 2b-AC2:** on the prototype layout, placing on a free cell returns `Placed`, blocks the cell and changes `Field` distances; removing restores the earlier distances exactly. `TryRemoveTower` returns false on a free cell and on a fixed wall, and leaves the wall blocked.
- **2b-AC3, each reason:** spawn and exit give `SpawnOrExit`; a wall and an existing tower give `CellBlocked`; a follower's `From` and its `To` give `EnemyInTheWay`; out of bounds gives `OutOfBounds`.
- **2b-AC3, path:** on the prototype layout, a tower on (5,7) is `Placed` and then (5,8) is `WouldBlockPath`. In a layout with two corridors, close the entrance behind a follower in one corridor (valid), then try the cell ahead of it: `WouldBlockPath`, although the spawn still reaches the exit.
- **2b-AC3, priority and no change:** a cell that fits two reasons returns the earlier one. After every refusal, `IsBlocked`, `HasTower` and every distance are what they were before.
- **2b-AC5:** with `System.Random` on a fixed seed, interleave a few thousand place-or-remove attempts on random cells with `Step` calls, restarting the follower on arrival. After every call assert: `From` and `To` are not blocked, `To` is reachable, and the follower is not standing still short of the exit. At the end, stop clicking and assert it arrives within `Distance` steps.
- **2b-AC6:** after `Restart(spawn)` the follower equals a newly constructed one.
- **2b-AC9:** the Core asmdef test still passes; the Gameplay asmdef JSON has no reference to Presentation.
- **2b-AC10:** `Is.Not.AllocatingGCMemory` for a placed, a refused and a removed call, and for `Step` and `Restart`, after a warm-up, with the fallback from plan 0001.
- **Data:** `WorldToCell(CellToWorld(cell)) == cell` for every cell, and a point just inside each edge of a cell maps to it.

**PlayMode**, in `Prototype.unity`, clicking through `TowerPlacementInput.Click(camera.WorldToScreenPoint(map.CellToWorld(cell)))`:
- **2b-AC1, 2b-AC2:** a click on a free cell creates `Tower x,y` on the cell centre and blocks the cell; a second click destroys it. With a tower on the S route, the enemy still reaches the exit and is never inside a blocked cell.
- **2b-AC3, 2b-AC4:** for each of the four reasons, the label `placement-message` shows the exact text and is hidden again after a little over 2 real seconds. A click outside the grid leaves it hidden.
- **2b-AC6:** at a raised `timeScale`, the enemy reaches the exit and is then at the spawn; the same `EnemyAgent` instance is the only one throughout.
- **2b-AC7:** after `Hover` on a free cell and on a tower cell, `Hover` is active on that cell centre; after `Hover` on a point off the grid it is inactive.
- **2b-AC8:** set `camera.aspect` to 1 and to 2.5, wait a frame, and assert the camera's orthographic bounds contain the whole grid. Call `ResetAspect` in TearDown.
- **2a-AC4** still passes with the tower and hover materials.

**Orchestrator, then Eduardo (2b-AC11 and the feel):** real mouse input in the editor and at `http://localhost:8765`, the text is legible, and the highlight is visible but not distracting.

## Risks
- **First WebGL build.** `unity build` without `--execute-method` is unproven in this project; T2a-3 names the fallback. The build may leave WebGL as the active platform when the editor reopens, which costs one reimport and no more.
- **Stale browser cache.** The player caches its data in the browser. After a rebuild, reload without cache before judging the result.
- **UI Toolkit in the player.** A panel with no theme shows no text. T2b-2 assigns the theme, and the browser check in 2b is the proof.
- **Experimental `com.unity.pipeline` package.** Its runtime assembly is compiled out of players by its define constraints; if the build log blames it, report it instead of working around it.
- **Coupling.** The remove-or-place choice and the respawn call are the only decisions allowed in MonoBehaviours; the Stage 4 review checks that the rule stays in `Board`.
- **GC.** A click runs at most two searches over arrays allocated once. The events use `Action<T>` with value types, so raising them does not allocate.
- **WebGL performance.** Negligible: under 300 renderers on the SRP Batcher, one transparent quad and one label.
- **Mouse only.** `Mouse.current` is null on a touch device, so the build does nothing there; touch is out of scope.
