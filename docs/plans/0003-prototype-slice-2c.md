# Technical plan 0003 – Prototype slice 2, step 2c

Towers shoot and kill; the enemy has health. Architecture: `docs/adr/0003-simulation-tick-pooling-combat-views.md`, on top of ADR 0001 and 0002. Prototype mode: this plan carries the acceptance criteria.

## Approach
Core gains a second class next to `Board`. `Board` keeps the state: it now holds one `Tower` object per cell instead of a `bool`, the enemies on the board instead of bare followers, and raises the tower events itself. `Battle` holds time: `Tick()` advances everything by a constant 1/60 s in a fixed order. The bootstrap is the only driver; it runs zero or more ticks per frame from an accumulator, so the number of shots in a pass no longer depends on the frame rate. A variable step and `FixedUpdate` were discarded; ADR 0003 says why.

Views only draw. `EnemyAgent` leaves Gameplay and becomes `EnemyView` in Presentation, and a new `CombatView` reads the `Battle` state every frame: it places the enemy, scales its health bar and shows the n-th pooled projectile view on the n-th projectile, all interpolated between the last two ticks. There are no per-shot events and nothing is instantiated per shot.

The enemy's loop stays the prototype's stand-in for waves, now inside `Battle`: a death and an exit are one path. The enemy leaves the board and re-enters at the spawn with full health after a delay. **This replaces 2b's instant restart at the exit.** It goes away in slice 3.

## Acceptance criteria
- **2c-AC1 Firing.** A tower with no cooldown left fires at once when an enemy is in range, then once every `1 / shotsPerSecond` seconds while one stays in range, never more than once per tick. Range is the distance in cells from the tower's cell centre to the enemy's position, inclusive. There is no line of sight.
- **2c-AC2 Targeting.** Among the enemies on the board and in range, a tower picks the one with the lowest distance to the exit along the flow field (the distance of the cell it is heading to, plus the unfinished part of its step). On a tie it picks the one that entered first.
- **2c-AC3 Projectile.** A shot starts at the tower's cell centre and moves towards its target's current position at the tower's projectile speed. It hits when the target is within one step, and removes the tower's damage from the enemy's health. If the target has left the board, the projectile vanishes with no effect. Removing a tower leaves its projectiles flying.
- **2c-AC4 Leaving and re-entering.** At health 0 or below, or on reaching the exit, the enemy leaves the board: it is not drawn, cannot be targeted and does not count for the placement rule. After the re-entry delay (1 s, a serialized field on the bootstrap) it enters at the spawn with full health and walks the current route.
- **2c-AC5 Readable.** Every projectile is visible from the tower to the enemy. A bar above the enemy shows health over maximum health: full on entry, shorter after each hit.
- **2c-AC6 Pooling.** After start-up, play instantiates only tower visuals, and destroys only those. There is exactly one enemy view throughout. Projectile views come from a pool pre-warmed to 16, which grows only when more are in flight.
- **2c-AC7 2b still holds.** 2b-AC1 to AC5, AC7 and AC8 are unchanged, including while the enemy is off the board, when only the spawn must still reach the exit.
- **2c-AC8 Data.** Range, damage, shots per second and projectile speed come only from `Tower_Prototype.asset`; health and speed only from `Enemy_Prototype.asset`. Values: range 2.5 cells, damage 10, 1.5 shots/s, projectile speed 10 cells/s; health 100, speed 2 cells/s.
- **2c-AC9 Provisional design target.** With those values on the prototype map, no single tower on any free cell kills the enemy in one pass, and towers on (6,6) and (11,2) do. The `balance-designer` replaces the values and this criterion later.
- **2c-AC10 Fixed tick.** Core advances only through `Battle.Tick()`, in steps of 1/60 s and in the order of T2c-1. The bootstrap is its only caller in the game. The same placements at the same ticks always give the same result.
- **2c-AC11 Smooth motion.** Enemy and projectile views are drawn between their last two tick positions, so an enemy on a straight stretch moves on every frame, in proportion to the frame time.
- **2c-AC12 Layers.** Every combat rule is in `ElementalTD.Core`, which still compiles with `noEngineReferences: true`. Gameplay instantiates nothing. Tower changes are announced only by `Board`.
- **2c-AC13 Allocation.** After warm-up, `Battle.Tick()` allocates 0 bytes, including ticks with a shot, a hit, a death and a re-entry. So do a refused placement and a removal. A successful placement may allocate one Core tower object (this relaxes 2b-AC10). The per-frame view code has no `new` of a reference type, LINQ, closure, boxing or string building.
- **2c-AC14 Range disc (droppable).** While the hover highlight shows, a translucent disc centred on the hovered cell shows the tower's range.
- **2c-AC15 WebGL.** All of the above works in the build at `http://localhost:8765`, in Chrome and Safari.

## Editor sequencing
Eduardo may have the interactive editor open. The `unity-dev` drives it only after the orchestrator hands it over, and nobody closes it without the orchestrator's word.

| Order | Work | Editor |
|---|---|---|
| 1 | T2c-1 to T2c-5, and the code part of T2c-7 | Code only. The editor may stay Eduardo's. |
| 2 | T2c-6, and the asset part of T2c-7 | Open, after the hand-over. Save everything at the end. |
| 3 | Stage 4 review, then Stage 5 tests | On an APFS clone while the editor is open (`docs/unity-cli-notes.md`, Tests). |
| 4 | T2c-8, serve, browser check | Closed. The build needs the real project. |

**Order 1 is a problem for Eduardo's editor, not for any task.** His editor imports the files as they land. Until T2c-5 is done the project does not compile there, so it cannot enter Play. From then until T2c-6 it compiles but is half wired: the enemy is not drawn and clicks place nothing. Nothing is lost, because order 1 touches no scene, prefab or asset. If Eduardo needs a working editor meanwhile, do order 1 on an APFS clone and copy `Assets/_Game/` back at the hand-over.

New scripts get their `.meta` from the editor on its next refresh; before order 2 ends, every new script must have one. Order 1 is checked without the editor: on an APFS clone, a batch EditMode run compiles every assembly.

## Tasks

### T2c-1 – Core: enemies, towers, projectiles and the tick
Serves 2c-AC1 to AC4, AC10, AC12 and AC13. Code only; in parallel with T2c-2.

Positions in Core are in cell coordinates: a cell's centre has that cell's integer `(x, y)`.

New files in `Assets/_Game/Core/`:

```csharp
public readonly struct EnemyStats { public readonly float Speed, MaxHealth; }                        // plus a constructor
public readonly struct TowerStats { public readonly float Range, Damage, ShotsPerSecond, ProjectileSpeed; }

public sealed class Enemy
{
    public Enemy(FlowField field, GridPos start, in EnemyStats stats);
    public PathFollower Follower { get; }
    public float Speed { get; }
    public float MaxHealth { get; }
    public float Health { get; }        // keeps its last value while off the board
    public bool IsOnBoard { get; }
    public float X { get; }  public float Y { get; }
    public float PreviousX { get; }  public float PreviousY { get; }   // position at the previous tick
}

public sealed class Tower
{
    public GridPos Cell { get; }
    public TowerStats Stats { get; }    // the cooldown is internal
}

public sealed class Projectile          // target, damage and speed are internal
{
    public float X { get; }  public float Y { get; }
    public float PreviousX { get; }  public float PreviousY { get; }
}

public sealed class Battle
{
    public const float TickSeconds = 1f / 60f;
    public Battle(Board board, in EnemyStats enemy, float reentryDelaySeconds);  // the one enemy enters at once
    public Board Board { get; }
    public int ProjectileCount { get; }
    public Projectile ProjectileAt(int index);
    public void Tick();
}
```

Changes:
- `PathFollower.cs`: add `float DistanceToExit`, which is `field.Distance(To) + (1 - Progress)`, and 0 on arrival.
- `Board.cs`:

```csharp
public event Action<GridPos> TowerPlaced;    // raised after the board has changed
public event Action<GridPos> TowerRemoved;
public int TowerCount { get; }
public Tower TowerAt(int index);             // placement order
public int EnemyCount { get; }
public Enemy EnemyAt(int index);             // entry order
public PlacementResult TryPlaceTower(GridPos cell, in TowerStats stats);
public void AddEnemy(Enemy enemy);           // enters at Spawn with full health, previous position = position
public void RemoveEnemy(Enemy enemy);
```

  `AddFollower` and the `bool[]` go. `HasTower` and `TryRemoveTower` keep their signatures. The placement rule reads `From` and `To` of the enemies on the board.

`Battle.Tick()` does these four steps, in this order:
1. **Re-entry.** If the enemy is off the board, its timer loses one tick; at 0 or below, `Board.AddEnemy`.
2. **Enemies**, in entry order. Store the previous position, step the follower by `TickSeconds`, update the position. On arrival the enemy leaves: `Board.RemoveEnemy` and the timer starts.
3. **Towers**, in placement order. The cooldown loses one tick. At 0 or below: with a target (2c-AC1, 2c-AC2) it takes a projectile from the free list, puts it on its cell and adds `1 / ShotsPerSecond` to the cooldown; with none the cooldown is set to 0.
4. **Projectiles.** If the target is off the board, release the projectile. Otherwise store the previous position; if the target is within `ProjectileSpeed × TickSeconds`, apply the damage and release, else move that far towards it. An enemy at health 0 or below leaves as in step 2.

**Done when:** Core has no `UnityEngine`; no `new` of a reference type, LINQ, closure or boxing in `Tick` after the free list has grown; no collection is iterated in hash order.

### T2c-2 – Data: definitions
Serves 2c-AC8. Code only; in parallel with T2c-1.

In `Assets/_Game/Data/`:
- New `TowerDefinition.cs` (ScriptableObject, menu `ElementalTD/Tower Definition`): `rangeCells`, `damage`, `shotsPerSecond` and `projectileSpeedCellsPerSecond`, the last two above 0, and `TowerStats ToStats()`.
- `EnemyDefinition.cs`: add `maxHealth` (at least 1), `MaxHealth` and `EnemyStats ToStats()`.
- `MapDefinition.cs`: add `Vector3 CellToWorld(float x, float y)` for Core positions; the `GridPos` overload calls it.

**Done when:** the files exist and hold no number other than neutral field defaults.

### T2c-3 – Gameplay: the driver and the input
Serves 2c-AC4, AC8, AC10 and AC12. Code only; after T2c-1 and T2c-2.

- `PrototypeBootstrap.cs`:
  - Replace the `enemyPrefab` field with `reentryDelaySeconds`. `Awake` builds the `Board` and then the `Battle`; when the spawn cannot reach the exit it logs the present error and leaves `Battle` null. `Start` goes.
  - `Update` adds `Time.deltaTime` to an accumulator, calls `Battle.Tick()` while a whole tick is left, and stores the rest.
  - Expose `Battle` and `float TickAlpha` (the rest as a fraction of a tick, 0 to 1).
- `TowerPlacementInput.cs`: add a serialized `TowerDefinition tower` with a read-only `Tower` property; pass `tower.ToStats()` to `TryPlaceTower`; pick nothing while `tower` is null. Remove `TowerPlaced` and `TowerRemoved`; `PlacementRefused` stays.
- `EnemyAgent.cs` leaves Gameplay (T2c-4).

**Done when:** no MonoBehaviour in Gameplay steps a follower, instantiates anything or holds a rule beyond the remove-or-place choice.

### T2c-4 – Presentation: views
Serves 2c-AC5, AC6, AC11, AC12 and AC13. Code only; after T2c-3.

- Move `Gameplay/EnemyAgent.cs` **with its `.meta`** to `Presentation/EnemyView.cs`, so that `Enemy.prefab` keeps its script reference, and rewrite it: a serialized `Transform healthFill` and `void Show(Vector3 position, float healthFraction)`, which sets the position and, only when the fraction changed, the x scale of `healthFill`.
- New `CombatView.cs`, with serialized references to the bootstrap, the enemy prefab (`EnemyView`), the projectile prefab and a pre-warm count:
  - `Start` instantiates one enemy view named `Enemy` and the pre-warmed projectile views, inactive, under a root named `Projectiles`, all scaled by the cell size. With a missing reference or a null `Battle` it logs one error and disables itself.
  - `LateUpdate` shows the enemy view on enemy 0 while `Board.EnemyCount` is above 0, and hides it otherwise. It shows view n on projectile n, hides the rest, and instantiates a view only when the pool is too small. Every position is `Lerp(previous, current, TickAlpha)` through `MapDefinition.CellToWorld`. It toggles `SetActive` only on a change.
- `BoardView.cs`: subscribe to `Board.TowerPlaced` and `Board.TowerRemoved` instead of the input's events.

One enemy view on enemy 0 is the prototype's limit; slice 3 pools views per enemy.

**Done when:** on an APFS clone, a batch run reports compile errors only under `Tests/` (they wait for T2c-5).

### T2c-5 – Keep the 2b tests compiling (`qa-tester`)
Serves 2c-AC7 and AC13. Code only; after T2c-4. Mechanical changes only; the new tests are Stage 5.

- `BoardTests.cs`, `PathFollowerAllocationTests.cs`: replace `board.AddFollower()` by `new Enemy(board.Field, board.Spawn, stats)` plus `board.AddEnemy(enemy)`, and step `enemy.Follower` as before. Give `TryPlaceTower` any `TowerStats`.
- `PathFollowerAllocationTests.cs`: drop the zero-allocation assertion for a successful placement (2c-AC13); keep those for a refusal, a removal, `Step` and `Restart`.
- `PrototypeSceneTests.cs`: `EnemyAgent` becomes `EnemyView`, searched with inactive objects included.

**Done when:** on an APFS clone a batch EditMode run compiles every assembly, and the EditMode suite passes.

### T2c-6 – Assets and scene wiring
Serves 2c-AC5, AC6, AC8 and AC9. Editor open, after the hand-over; after T2c-5.

Through `unity command`, each asset with its `.meta` and no collider on any primitive:
- `Data/Assets/Tower_Prototype.asset` with the values of 2c-AC8; `maxHealth` 100 on `Enemy_Prototype.asset`.
- `Presentation/Materials/`: `Projectile` (a bright colour), `HealthBar_Back` (dark) and `HealthBar_Fill` (green), all URP Unlit, opaque.
- `Presentation/Prefabs/Projectile.prefab`: a root with a child sphere of scale 0.2 at local y 0.8, above the tower tops.
- `Presentation/Prefabs/Enemy.prefab`:
  - `Body` stays the first child; an existing test reads the first renderer.
  - Add `HealthBar` at local (0, 1, 0.5). Under it: `Back`, a flat quad 0.8 wide and 0.12 deep; and `FillPivot` at the bar's left end, with a child `Fill` of the same size, offset half its width to the right and a hair above `Back`.
  - Assign `FillPivot` to `healthFill`.
- `Prototype.unity`: add an object `Combat View` with `CombatView` (bootstrap, both prefabs, pre-warm 16); set `reentryDelaySeconds` to 1 on the bootstrap and `tower` to `Tower_Prototype` on the input; save the scene.

**Done when:** clean compile with an empty console; in Play, towers shoot the enemy in range, the bar shrinks, the enemy vanishes at 0 and is back at the spawn a second later with a full bar; 2b's placement, removal, messages and hover work as before; everything is saved.

### T2c-7 – Range disc (droppable)
Serves 2c-AC14 only. Dropping it removes this task and that criterion, and touches nothing else.

- Code only, with order 1: `BoardView.cs` gets a serialized `rangeDiscPrefab` and keeps one instance named `RangeDisc`. It scales it on x and z once, by the range of `placementInput.Tower` times the cell size, and moves and toggles it together with the hover.
- Editor open, at the end of T2c-6: `Materials/RangeDisc` (URP Unlit, transparent, alpha about 0.15); `Prefabs/RangeDisc.prefab`, a root with a child flat cylinder of radius 1 just above the tiles; assign it on `Board View`; save.

**Done when:** in Play the disc follows the hover, reaches 2.5 cells from the cell centre and hides off the grid.

### T2c-8 – WebGL rebuild
Serves 2c-AC15. Editor closed, on the real project; after the tests pass.

Run `tools/webgl.sh`. **Done when:** it exits with 0 and `http://localhost:8765` serves the new build.

## Tests for the qa-tester
While the interactive editor is open, run on an APFS clone (`docs/unity-cli-notes.md`, Tests). The EditMode tests need no scene, so they can be written and run as soon as T2c-5 is done.

**EditMode**, on `Board` and `Battle`, with stats chosen per test:
- **2c-AC1:** an enemy exactly at the range is shot, and one just beyond is not; a tower behind a wall shoots. A tower placed next to a waiting enemy fires on the first tick. Over 60 s with a target always in range, 1.5 shots/s gives 90 shots, give or take one.
- **2c-AC2:** with two enemies in range, the target is the one nearer the exit, including when they share a cell and differ only in progress; on an exact tie, the first to enter.
- **2c-AC3:** a projectile starts on the tower's cell, gets closer every tick and hits; the health drops by exactly the damage, once. A second projectile in flight when the first one kills vanishes, and the enemy re-enters with full health. After `TryRemoveTower`, that tower's projectile still hits.
- **2c-AC4:** at health 0 and on arrival, `EnemyCount` is 0 and `IsOnBoard` is false; a cell the enemy stood on can be built on. With a delay of 1 s, 60 ticks later (give or take one) the enemy is on the spawn with `Health == MaxHealth`. A tower placed on the old route during the gap is respected on re-entry.
- **2c-AC7:** the 2b `Board` suite passes after T2c-5. `TowerPlaced` and `TowerRemoved` fire once per change, after the change, and never on a refusal.
- **2c-AC9, named as a provisional design-target check to retune with the `balance-designer`:** load the three prototype assets. For every free cell where the placement is accepted, place one tower and tick until the enemy leaves: its `Health` is above 0. With towers on (6,6) and (11,2), it leaves with `Health` at 0 or below.
- **2c-AC10:** two battles given the same placements at the same ticks have identical health, positions and projectile counts after every tick, for a few thousand ticks.
- **2c-AC12:** the Core asmdef and source tests still pass; no source file under `Gameplay/` contains `Instantiate`.
- **2c-AC13:** after a warm-up that covers a shot, a death and a re-entry, `Is.Not.AllocatingGCMemory` over a few hundred ticks that cover them again.
- **Data:** `ToStats()` returns the asset's values; `CellToWorld(x, y)` agrees with the `GridPos` overload.

**PlayMode**, in `Prototype.unity`, at a raised `timeScale`:
- **2c-AC4, AC6 (rewrites the 2b-AC6 test):** the enemy reaches the exit, its view is inactive for about one second of game time, then it is active on the spawn. Counting inactive objects, the same single `EnemyView` exists throughout.
- **2c-AC5:** with a tower beside the route, an active projectile view appears between the tower and the enemy, and the fill's x scale drops below 1 and equals `Health / MaxHealth`. With towers on (6,6) and (11,2) the enemy view goes inactive before the exit and returns with the fill at 1.
- **2c-AC6:** after a few passes with three towers, the number of projectile views is still the pre-warm count and no object named `Enemy` or `Projectile` was created or destroyed.
- **2c-AC11:** at `timeScale` 1, over 60 frames on a straight stretch, the enemy view moves on every frame, and by `speed × Time.deltaTime` within a small tolerance.
- **2c-AC14:** on hover the object `RangeDisc` is active, centred on the cell and 2.5 cells in radius; off the grid it is inactive.
- **2c-AC7:** the 2b PlayMode tests pass. Check each one that places towers and waits for the enemy: a tower now shoots, so use a single tower or do not depend on the enemy surviving.
- **2a-AC4:** the material, shader and no-collider tests pass with the new assets.

**Orchestrator in the browser, then Eduardo (2c-AC15 and what no test can judge)**, at `http://localhost:8765` after a reload without cache, in Chrome and Safari:
- Projectiles are easy to see against the tiles and towers, and both they and the enemy move without stutter.
- The health bar is readable at this zoom and shrinks in clear steps.
- The range disc follows the mouse, is visible but not distracting, and matches where towers start shooting.
- With one tower the enemy survives the pass. With (6,6) and (11,2) it dies, disappears, and returns at the spawn about a second later with a full bar.
- 2b still works: place, remove, the four refusal messages, the hover, and re-routing around towers.
- The browser console shows no error.

## Risks
- **Eduardo's editor during order 1.** See Editor sequencing.
- **Test churn.** 2c changes the API under 2b's suite; T2c-5 lists the changes, and Stage 5 must rerun all of it.
- **Stray colliders.** Primitives created in the editor arrive with a collider, which fails an existing test.
- **Projectile slower than its target.** It would trail the enemy until the enemy leaves. The data keeps projectile speed far above enemy speed; nothing enforces it.
- **GC.** A projectile object or view is created only when more are in flight than ever before. A click allocates one tower object and its visual.
- **WebGL performance.** Negligible: one tick is one follower step, a few range tests and a few projectile moves; about twenty more renderers on the SRP Batcher, and one transparent disc.
- **Long frames.** After a stall the driver runs several ticks in one frame. Unity caps `Time.deltaTime`, so the catch-up is bounded.
- **Coupling.** `CombatView` reads `Battle` directly and draws enemy 0. That is deliberate for one enemy and is replaced in slice 3.
