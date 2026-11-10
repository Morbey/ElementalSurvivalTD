# Technical plan 0001 – Prototype slice 1

A grid and one enemy crossing it with pathfinding. Architecture decisions and deferrals: `docs/adr/0001-folder-structure-asmdefs-layers.md`.

## Approach
Pathfinding is one flow field: a breadth-first search outward from the exit over a 4-neighbour grid, stored as a single `int[]` of distances. Each enemy steps to the neighbour with the lowest distance; on a tie it takes the first in a fixed order (+x, -x, +y, -y), so the route is the same every run. A* was discarded because it would be rerun per enemy and replaced as soon as enemies multiply. In slice 2, recalculating after a tower blocks a cell is running the flow field again, and the "never block the path" check is "the spawn cell's distance is still finite".

All rules live in `ElementalTD.Core`. `PathFollower` holds the enemy's state: the cell it left (`From`), the cell it is heading to (`To`) and how far along it is (`Progress`, 0 to 1). Core exposes only grid coordinates; Gameplay turns them into world positions by interpolating from `From` to `To`.

`ElementalTD.Data` holds two ScriptableObjects that convert to Core types. `ElementalTD.Gameplay` has a bootstrap that only wires things together and an agent that only converts positions.

## Acceptance criteria
- **AC1** – Pressing Play in `Prototype.unity` shows the grid through a static, straight-down orthographic camera framed to the grid. Free cells, blocked cells, the spawn cell and the exit cell are each visibly different.
- **AC2** – One enemy (a primitive) appears at the spawn cell when Play starts.
- **AC3** – The enemy moves at its configured speed along a shortest 4-neighbour path, cell centre to cell centre. It never enters a blocked cell and routes around both walls in an S shape.
- **AC4** – When the enemy reaches the exit cell it despawns, and nothing else happens.
- **AC5** – Grid width and height, cell size, blocked cells, spawn, exit and enemy speed come only from assets. Changing an asset changes the result with no code change.
- **AC6** – `ElementalTD.Core` compiles with `noEngineReferences: true`.
- **AC7** – Once warmed up, `PathFollower.Step` allocates 0 bytes. An automated EditMode test checks this.

## Tasks

### T1 – Scaffolding
Serves AC6 and the test infrastructure. Runs first.
- **Change** `Packages/manifest.json`: add `"com.unity.test-framework": "1.8.0"`. Leave every other entry untouched, including Cinemachine.
- **Create**, each with its `.meta`:
  - `Assets/_Game/Core/ElementalTD.Core.asmdef`, with `noEngineReferences: true` and no references.
  - `Assets/_Game/Data/ElementalTD.Data.asmdef`, referencing Core.
  - `Assets/_Game/Gameplay/ElementalTD.Gameplay.asmdef`, referencing Core and Data.
  - `Assets/_Game/Tests/EditMode/ElementalTD.Core.Tests.asmdef`: references Core and Data, Editor platform only, `UNITY_INCLUDE_TESTS` define constraint, NUnit and TestRunner.
  - `Assets/_Game/Tests/PlayMode/ElementalTD.PlayModeTests.asmdef`: references Core, Data and Gameplay, `UNITY_INCLUDE_TESTS`, NUnit and TestRunner.
  - The empty folders `Assets/_Game/Data/Assets/` and `Assets/_Game/Scenes/`.
- No test code.
- **Done when:** the project compiles cleanly. Unity does not compile an asmdef with no scripts, so the two test assemblies only appear once the `qa-tester` adds the first test to each.

### T2 – Core: grid, flow field, path follower
Serves AC3, AC4, AC6 and AC7. Runs after T1, in parallel with T3.

Create in `Assets/_Game/Core/`, namespace `ElementalTD.Core`:
- `GridPos.cs`: a readonly struct with `int X` and `int Y`, implementing `IEquatable<GridPos>`.
- `GridMap.cs`:
  - Constructed from `(int width, int height, IReadOnlyList<GridPos> blocked)`; stores the blocked cells in a `bool[]`.
  - `bool InBounds(GridPos)`, `bool IsBlocked(GridPos)`, `int Index(GridPos)`.
  - Throws `ArgumentException` for a size below 1 and for a blocked cell out of bounds.
- `FlowField.cs`:
  - The constructor takes `(GridMap map)` and allocates the distance array and the BFS queue once.
  - `void Compute(GridPos exit)`: BFS with uniform cost, 4 neighbours, skipping blocked cells.
  - `bool IsReachable(GridPos)`: the distance is not `int.MaxValue`.
  - `int Distance(GridPos)`.
  - `bool TryGetNext(GridPos from, out GridPos next)`: the neighbour with the lowest distance, ties broken in the order +x, -x, +y, -y. Returns false at the exit or when `from` cannot reach it.
- `PathFollower.cs`:
  - The constructor takes `(FlowField field, GridPos start)`.
  - Exposes `GridPos From`, `GridPos To`, `float Progress` (0 to 1) and `bool HasArrived`.
  - `bool Step(float dt, float speedCellsPerSecond)` advances the follower, carries leftover progress over cell boundaries inside the same call and returns `HasArrived`.
  - At the exit, `From == To` and `HasArrived` is true.

**Done when:** Core compiles with `noEngineReferences`; `grep -r "UnityEngine" Assets/_Game/Core` is empty; there is no `new` of a reference type, LINQ, closure or boxing inside `Step` or `TryGetNext`.

### T3 – Data: definitions and assets
Serves AC5. Runs after T1, in parallel with T2.

Create in `Assets/_Game/Data/`, namespace `ElementalTD.Data`, both with a `[CreateAssetMenu]` entry:
- `MapDefinition.cs` (ScriptableObject):
  - Fields: `width`, `height`, `cellSize`, `spawn`, `exit` and `blockedCells` (`Vector2Int[]`).
  - `GridMap ToGridMap()`, `GridPos Spawn` and `GridPos Exit` as conversions to Core types.
  - `OnValidate` clamps the size to at least 1 and `cellSize` to above 0.
- `EnemyDefinition.cs` (ScriptableObject): field `speedCellsPerSecond`.

Create through `unity command`:
- `Assets/_Game/Data/Assets/Map_Prototype.asset`: size 16 x 9, `cellSize` 1, spawn (0,4), exit (15,4). Blocked cells: wall A from (5,0) to (5,6), leaving a gap at the top; wall B from (10,2) to (10,8), leaving a gap at the bottom.
- `Assets/_Game/Data/Assets/Enemy_Prototype.asset`: `speedCellsPerSecond` 2.

**Done when:** both assets exist with these values and `ToGridMap()` round-trips the 14 blocked cells.

### T4 – Gameplay, scene and template cleanup
Serves AC1 to AC5. Runs after T2 and T3.

Create in `Assets/_Game/Gameplay/`, namespace `ElementalTD.Gameplay`:
- `PrototypeBootstrap.cs`, with serialized references to `MapDefinition`, `EnemyDefinition` and `Camera`. It holds no rules. In `Start` it:
  1. builds the `GridMap` and the `FlowField`, then calls `Compute(exit)`;
  2. creates one primitive per cell (a flattened cube or quad), with one shared material per kind: free, blocked, spawn and exit;
  3. frames the orthographic camera to the grid: straight down, centred, with `orthographicSize` taken from the grid size and the screen aspect;
  4. logs an error and stops if the spawn cannot reach the exit, so the grid stays visible;
  5. spawns one capsule with an `EnemyAgent`.
- `EnemyAgent.cs`:
  - `Init(PathFollower follower, float speed, MapDefinition map)` caches everything.
  - In `Update` it calls `follower.Step(Time.deltaTime, speed)` and sets `transform.position` by interpolating the world centres of `From` and `To` using `Progress`, allocating nothing.
  - When the follower arrives, it calls `Destroy(gameObject)`.
- Grid-to-world conversion: cell (x, y) maps to world (x·cellSize, 0, y·cellSize) plus half a cell. Write it once, in a static helper or a `MapDefinition` method.

Through the editor:
- Create `Assets/_Game/Scenes/Prototype.unity` with a camera, a directional light and one GameObject with `PrototypeBootstrap`, its references assigned to the two assets.
- Make that scene the only entry in Build Settings.
- Delete `Assets/Scenes/SampleScene.unity`, `Assets/TutorialInfo/` and `Assets/Readme.asset`, each with its `.meta`, plus `Assets/Scenes/` once it is empty. Keep `Assets/Settings/` and `Assets/InputSystem_Actions.inputactions`.

**Done when:** the project compiles cleanly; pressing Play shows the grid and an enemy that follows the S route and despawns at the exit in roughly 13 to 14 seconds; the console shows no errors; changing `speedCellsPerSecond` or a blocked cell in the assets visibly changes the run.

## Tests for the qa-tester

**EditMode** (`Assets/_Game/Tests/EditMode/`):
- **AC3, empty grid:** `Distance` equals the Manhattan distance to the exit for every cell.
- **AC3, prototype layout**, built in code. Following `TryGetNext` from the spawn never lands on a blocked cell, moves only to 4-neighbours, ends at the exit and takes `Distance(spawn)` steps. Hand-count that value and assert it as a constant, so a wrong BFS cannot pass.
- **Blocked spawn:** a spawn walled in on all four sides gives `IsReachable == false`, and `TryGetNext` returns false.
- **Edge cases:** spawn equal to exit gives `HasArrived` on the first `Step`; `GridMap` rejects a size of 0 and a blocked cell out of bounds.
- **AC3 and AC4, timing:** on a straight path of N cells at speed s, stepping with a fixed dt reaches `HasArrived` after N/s seconds, give or take one dt. One large dt that covers several cells in a single call still arrives at the right cell.
- **AC5:** a `MapDefinition` made with `ScriptableObject.CreateInstance` gives a `ToGridMap()` that matches its fields. It has no setters: fill it through `SerializedObject` or `JsonUtility.FromJsonOverwrite`, and add no test-only setters.
- **AC6:** the Core asmdef JSON has `noEngineReferences == true` and an empty `references` list.
- **AC7:** build the prototype map, the flow field and a follower, call `Step` once to warm up, then assert `Assert.That(() => follower.Step(0.016f, 2f), Is.Not.AllocatingGCMemory());`. Repeat across a cell boundary. If it is flaky in the editor, measure 1000 calls with `GC.GetAllocatedBytesForCurrentThread()` before and after instead.

**PlayMode** (`Assets/_Game/Tests/PlayMode/`), smoke test for AC2 to AC4:
1. Set `Time.timeScale = 0` and load `Prototype.unity`. The agent's first `Update` runs in the same frame as the bootstrap's `Start`, so without this it has already moved.
2. Assert that exactly one `EnemyAgent` exists and that it starts at the spawn cell centre.
3. Set `Time.timeScale = 10` and wait until no `EnemyAgent` is left, with a timeout of 5 real seconds.
4. Each frame, assert the enemy is never inside a blocked cell's bounds.
5. Reset `timeScale` in TearDown.

**Manual check by Eduardo** (AC1 and the look of AC3): the grid is readable, the four cell kinds are distinct and the S detour is obvious.

## Risks
- **Coupling.** Rules could creep into `PrototypeBootstrap` or `EnemyAgent`; the Stage 4 review checks that both contain only wiring and conversion.
- **GC test.** `Is.Not.AllocatingGCMemory` can fail falsely on the first call or in a Debug or Deep Profile run, hence the warm-up and the fallback above.
- **WebGL.** Negligible here: one BFS over 144 cells and about 150 primitives on the SRP Batcher.
