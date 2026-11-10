# Technical plan 0004 – Prototype slice 3, step 3a

Ten automatic waves, lives lost at the exit, and a win or lose screen with a restart. Architecture: `docs/adr/0004-run-waves-outcome-restart.md`, on top of ADR 0001 to 0003. Prototype mode: this plan carries the acceptance criteria. Gold (3b) and selling (3c) are not planned here.

## Approach
The looping enemy of 2c becomes a run. A new asset, `RunDefinition`, holds the lives and the ten waves. Core gets a small `WaveSchedule` that counts the delays and says when an enemy enters, and `Battle` gains the lives and an outcome: its tick is now waves, enemies, towers, projectiles, outcome. The re-entry goes completely. Enemy objects are reused through a free list on `Board`, as projectiles already are in `Battle`, so a spawn creates nothing once the pool has grown.

Presentation draws every enemy the way it draws projectiles: the n-th pooled enemy view shows the n-th enemy on the board. That removes the "enemy 0 only" limit of 2c with no map from enemies to views. One new view, `RunHudView`, polls the battle for the wave, the lives, the countdown and the end screen.

A restart reloads the scene, so no state can leak from one run into the next. Discarded: resetting the board and the battle in place, one asset per wave, wave timing inside `Battle`, a second enemy type, and GPU instancing; ADR 0004 says why. All ten waves use the one prototype enemy and only the count grows: a tougher enemy would need a second look and a view pool per prefab, which waits for slice 4.

## Acceptance criteria
- **3a-AC1 Waves.** A run plays the waves of its `RunDefinition` in order. A wave's delay counts from the start of the run (wave 1) or from the tick the last enemy of the previous wave entered, whether or not enemies are still alive. When it ends, the wave's first enemy enters at the spawn with full health, and the others follow one per spawn interval. At most one enemy enters per tick. Each enemy has the speed and health of its wave's enemy definition.
- **3a-AC2 Gone for good.** An enemy at health 0 or below leaves the board and never returns. Nothing of the 2c re-entry is left: no field, no code, no test.
- **3a-AC3 Lives.** The run starts with the lives of its `RunDefinition`. An enemy that reaches the exit leaves the board and costs one life. Lives never go below 0.
- **3a-AC4 Outcome.** At the end of every tick: the run is lost if no life is left; otherwise it is won if the last enemy of the last wave has entered and no enemy is on the board. An enemy that exits as the last one, with a life to spare, wins the run.
- **3a-AC5 The run stops.** Once won or lost, `Battle.Tick()` changes nothing, no view moves, the hover hides and a click on the board does nothing. A won run has no projectile left.
- **3a-AC6 End screen.** While the run is running the end screen is hidden. After it ends, a screen over the board says "Victory" or "Defeat" and has a "Restart" button.
- **3a-AC7 Restart.** Clicking Restart starts a fresh run: no tower, no enemy, no projectile, full lives, the first wave's whole delay ahead, and the end screen hidden. It works run after run, and the click places no tower.
- **3a-AC8 HUD.** During the run the screen shows "Wave N/T", "Lives L" and "Next wave in S s". N is the number of waves whose first enemy has entered (0 before the first), T the number of waves, S the seconds left rounded up. The countdown is hidden while a wave is spawning and once the last wave has started. The HUD does not cover the placement message.
- **3a-AC9 Every enemy is drawn.** Each enemy on the board has a view with its own health bar, drawn between its last two tick positions (2c-AC5, 2c-AC11). Enemy views come from a pool pre-warmed to 32, which grows only when more enemies are on the board. Within a run, after start-up, play instantiates and destroys only tower visuals. This replaces 2c-AC6.
- **3a-AC10 Combat with many enemies.** 2c-AC1 to AC3 hold with several enemies on the board. A shot whose target has left vanishes with no effect, even when a later enemy reuses that object.
- **3a-AC11 Placement.** 2b-AC1 to AC5 are unchanged and count every enemy on the board; with none on it, only the spawn must still reach the exit. Towers are free and unlimited, and a click on a tower removes it.
- **3a-AC12 Data.** Lives and waves come only from `Run_Prototype.asset`: 10 lives; ten waves of `Enemy_Prototype` with 3, 4, 5 … 12 enemies; a spawn interval of 1 s; a delay of 15 s before wave 1 and 8 s before the others. Every delay is within 5 to 20 s. No code holds one of these numbers.
- **3a-AC13 Provisional design target.** With those values on the prototype map: a player who builds nothing is defeated 50 to 53 s into the run, when the third enemy of wave 3 reaches the exit; the last enemy enters 152 s in; towers on (3,5), (6,6), (8,6), (8,3), (11,2), (13,2), (14,3) and (3,3), placed before wave 1, win with all 10 lives between 2 min 30 s and 3 min. The numbers come from a rough planning simulation; the `balance-designer` replaces them and this criterion in 3b. 2c-AC9 still holds for one enemy.
- **3a-AC14 Tick and layers.** `Battle.Tick()` runs waves, enemies, towers, projectiles and the outcome, in that order. Waves, lives and the outcome are rules of `ElementalTD.Core`, which still compiles with `noEngineReferences: true`. Gameplay instantiates nothing. A whole run with the same placements at the same ticks ends at the same tick with the same outcome (2c-AC10).
- **3a-AC15 Allocation.** `Battle.Tick()` allocates 0 bytes, also on a spawn, a kill, an exit and a lost life, except when more enemies or projectiles are on the board than ever before in the run. The per-frame view code has no `new` of a reference type, LINQ, closure or boxing, and builds a HUD text only when its number changes.
- **3a-AC16 WebGL.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari, with no visible stutter in wave 10. `http://localhost:8765` keeps serving the 2c build.

## Editor sequencing
The slice lives in a git worktree with its own `Library`. The team owns the editor on the worktree; the editor on Eduardo's checkout is never touched. Every `unity` call names the worktree by its full path, never by project name, and `unity status --project-path <worktree>` is checked before any `unity command`.

| Order | Work | Editor on the worktree |
|---|---|---|
| 1 | T3a-1, T3a-2 and T3a-8 in parallel; then T3a-3; then T3a-4 and T3a-5 in parallel; then T3a-6 | Closed. The compile check is a batch run, `unity test <worktree> --mode EditMode`, which also writes the `.meta` of each new script. |
| 2 | T3a-7 | Open: `unity projects open <worktree> --until-ready`. Each call is `unity command --project-path <worktree> …`. Save everything, then quit with `editor quit --save true`. |
| 3 | Stage 4 review, then Stage 5 tests | Closed. `unity test <worktree> --mode EditMode`, then `--mode PlayMode`. |
| 4 | T3a-9 and the browser check | Closed. |

## Tasks

### T3a-1 – Core: waves, lives and the outcome
Serves 3a-AC1 to AC5, AC10, AC14 and AC15. Code only; in parallel with T3a-2 and T3a-8.

New files in `Assets/_Game/Core/`:

```csharp
public enum RunOutcome { Running, Won, Lost }

public readonly struct Wave             // the constructor rejects a count below 1, an interval of 0 or less and a negative delay
{
    public readonly EnemyStats Enemy;
    public readonly int Count;
    public readonly float SpawnIntervalSeconds, DelayBeforeSeconds;
}

public sealed class WaveSchedule
{
    public WaveSchedule(Wave[] waves);                    // at least one wave
    public int WaveCount { get; }
    public int StartedWaves { get; }                      // waves whose first enemy has entered
    public float SecondsToNextWave { get; }               // above 0 only while a wave's delay runs
    public bool IsFinished { get; }                       // the last enemy of the last wave has entered
    public bool Step(float dt, out EnemyStats enemy);     // true when an enemy enters now
}
```

`WaveSchedule.Step` takes `dt` from one timer. At 0 or below an enemy enters. If its wave has more, the timer gains the spawn interval; otherwise the next wave starts counting and the timer gains its delay; after the last enemy of the last wave the schedule is finished.

Changes:
- `Battle.cs`: the constructor becomes `Battle(Board board, Wave[] waves, int lives)`, with at least one life. Add `WaveSchedule Waves`, `int Lives` and `RunOutcome Outcome`. Remove the single enemy, the re-entry delay, its timer and `StepReentry`. `Tick()` returns at once unless the outcome is `Running`, and otherwise does:
  1. **Waves.** If the schedule says an enemy enters, `Board.SpawnEnemy` with its stats.
  2. **Enemies**, as in 2c. On arrival the enemy is removed and a life is lost, never below 0.
  3. **Towers**, as in 2c.
  4. **Projectiles**, as in 2c. A killed enemy is removed.
  5. **Outcome**, by 3a-AC4. On a win the projectiles left are released.
- `Board.cs`: `AddEnemy` becomes `Enemy SpawnEnemy(in EnemyStats stats)`, which takes an enemy object from a free list, or creates one when it is empty, and enters it at the spawn. `RemoveEnemy` also returns the object to the free list.
- `Enemy.cs`: the constructor becomes internal and takes no stats; `Enter` takes them and sets speed, maximum health and health. The entry count stays: it is what 3a-AC10 relies on.

**Done when:** Core has no `UnityEngine`; nothing in Core mentions a re-entry; no `new` of a reference type, LINQ, closure or boxing in `Tick` after the free lists have grown.

### T3a-2 – Data: the run
Serves 3a-AC1, AC3 and AC12. Code only; in parallel with T3a-1.

New `Assets/_Game/Data/RunDefinition.cs` (ScriptableObject, menu `ElementalTD/Run Definition`): `lives` (at least 1) and an array of serializable wave entries, each with `enemy` (`EnemyDefinition`), `count`, `spawnIntervalSeconds` and `delayBeforeSeconds`. It exposes `int Lives` and `Wave[] ToWaves()`, which throws for a run with no wave or a wave with no enemy, as `MapDefinition.ToGridMap()` does for a bad map.

**Done when:** the file holds no number other than neutral field defaults.

### T3a-3 – Gameplay: the driver and the input
Serves 3a-AC5, AC7 and AC14. Code only; after T3a-1 and T3a-2.

- `PrototypeBootstrap.cs`: replace the `enemy` and `reentryDelaySeconds` fields by `RunDefinition run`, and build the battle from `run.ToWaves()` and `run.Lives`. `Update` does nothing once the outcome is not `Running`, and stops its tick loop on the tick that ends the run, so that `TickAlpha` no longer changes. Add `void Restart()`, which reloads the active scene.
- `TowerPlacementInput.cs`: pick no cell once the run is over, so the hover hides and a click does nothing.

**Done when:** Gameplay holds no rule beyond the remove-or-place choice and no number.

### T3a-4 – Presentation: a view per enemy
Serves 3a-AC9 and AC15. Code only; after T3a-3, in parallel with T3a-5.

`CombatView.cs`: replace the single enemy view by a pool, built like the projectile pool: a serialized `prewarmEnemies`, views named `Enemy` under a root named `Enemies`, view n shown on `Board.EnemyAt(n)` and the rest hidden, `SetActive` only on a change, and a new view only when the pool is too small. `EnemyView.cs` does not change.

**Done when:** no code path draws only enemy 0, and a view keeps nothing that belongs to one enemy.

### T3a-5 – Presentation: HUD and end screen
Serves 3a-AC6, AC7, AC8 and AC15. Code and UI text files; after T3a-3, in parallel with T3a-4.

- `UI/Hud.uxml` and `UI/Hud.uss`: labels `wave-label`, `lives-label` and `next-wave-label` in the top-left corner, ignored by picking; an element `end-screen` over the whole screen with a translucent dark background, a label `end-title` and a button `restart-button` with the text "Restart".
- New `RunHudView.cs`, with serialized references to the `UIDocument` and the bootstrap and the texts as serialized fields ("Wave {0}/{1}", "Lives {0}", "Next wave in {0} s", "Victory", "Defeat"). Each frame it compares the started waves, the lives, the whole seconds left and the outcome with what it last showed, and rewrites only what changed. The button calls `bootstrap.Restart()`. With a missing reference, element or battle it logs one error and disables itself.

**Done when:** the view holds no rule and no number, and unsubscribes from the button in `OnDestroy`.

### T3a-6 – Keep the 2c tests compiling (`qa-tester`)
Serves 3a-AC2, AC10 and AC11. Code only; the EditMode part after T3a-1 and T3a-2, the PlayMode part after T3a-4 and T3a-5. Mechanical changes only; the new tests are Stage 5.

- Every `new Enemy(…)` plus `board.AddEnemy(…)` becomes `board.SpawnEnemy(stats)`.
- `BattleTests.cs`: build the battle from a one-enemy wave with no delay and tick once, which gives the 2c starting state. A test that ticks on after its only enemy has left needs a later wave to keep the run running.
- Delete the tests that need a re-entry. In `BattleTests.cs`: the two 2c-AC4 re-entry tests, the 2c-AC3 "delay 0" test and the 2c-AC13 test. In `PrototypeSceneTests.cs`: the slice 1 "exactly one enemy" test, the 2c-AC4/AC6 test, the 2c-AC6 test and the "returns with a full bar" half of the 2c-AC5 test. Stage 5 covers their ground again under 3a-AC2, AC3, AC9, AC10 and AC15.
- `PrototypeSceneTests.cs`: the scene now starts with no enemy on the board. Add a helper that ticks until the first enemy has entered, and use it wherever a test needs an enemy.

**Done when:** a batch EditMode run compiles every assembly and the remaining EditMode tests pass.

### T3a-7 – Assets and scene wiring
Serves 3a-AC6, AC8, AC9 and AC12. Editor open on the worktree; after T3a-6.

Through `unity command` (the wave array through `script eval` with a `SerializedObject`):
- `Data/Assets/Run_Prototype.asset` with the values of 3a-AC12.
- `Prototype.unity`: `run` on the bootstrap; `prewarmEnemies` 32 on `Combat View`; `RunHudView` on the `Hud` object with its document, the bootstrap and the five texts. Save the scene, which also drops the two removed bootstrap fields from its file.

**Done when:** clean compile with an empty console; in Play the countdown runs, waves enter, lives drop at the exit, and both end screens appear; a real mouse click on Restart starts a new run (see Risks); everything is saved.

### T3a-8 – The WebGL script serves any port
Serves 3a-AC16. Code only; in parallel with everything.

- `tools/webgl.sh`: the port comes from `WEBGL_PORT`, 8765 by default. `serve` counts a server as its own only when the process in its PID file is alive; when the port answers but belongs to another server it fails and names `WEBGL_PORT`, instead of reporting the other build as this one.
- `docs/unity-cli-notes.md`: say that in Builds, and replace the APFS-clone bullet under Tests by the worktree rule of this plan's Editor sequencing.

**Done when:** in the worktree, `tools/webgl.sh serve` fails while 8765 serves the 2c build, `WEBGL_PORT=8766 tools/webgl.sh serve` serves the worktree's build, and `stop` ends only the server it started.

### T3a-9 – WebGL build
Serves 3a-AC16. Editor closed; after the tests pass.

Run `WEBGL_PORT=8766 tools/webgl.sh` in the worktree. The first build on the new `Library` takes minutes. **Done when:** it exits with 0 and `http://localhost:8766` serves the new build.

## Tests for the qa-tester
All on the worktree with its editor closed. The EditMode tests need no scene, so they can be written as soon as T3a-1 and T3a-2 are done.

**EditMode**, on `WaveSchedule`, `Board` and `Battle`, with waves chosen per test:
- **3a-AC1, schedule alone:** with a delay of 15 s the first enemy enters on tick 900, give or take one, and the others one per interval; the next wave's delay starts on the tick of the previous wave's last enemy; a delay of 0 or an interval below one tick still gives one enemy per tick. `StartedWaves`, `SecondsToNextWave` and `IsFinished` hold their documented values before, during and after each wave. Invalid waves, an empty list and zero lives throw.
- **3a-AC1, stats:** two waves with different stats give each enemy its wave's speed and health, also when the object is reused.
- **3a-AC2:** after a kill the enemy count stays down until the next wave, and over a whole run exactly the sum of the counts enters. No source file outside `Tests/` and no scene contains `reentry`, in any casing.
- **3a-AC3, AC4:** each exit costs one life. The run is lost on the tick the last life goes, also when that enemy is the last of the run. It is won on the tick the last enemy is killed, or exits with a life to spare, and not while a wave is still to come.
- **3a-AC5:** after either outcome, more ticks change no life, position, count or timer; after a win the projectile count is 0.
- **3a-AC10:** the 2c firing, targeting and projectile tests pass with three or more enemies on the board. A slow shot aimed at an enemy that is then killed vanishes, and the next enemy, which reuses the object, keeps full health.
- **3a-AC11:** between waves a placement that keeps the spawn connected is accepted; with enemies in two corridors, cutting off either one is refused. The 2b `Board` suite passes.
- **3a-AC12:** `Run_Prototype.asset` holds the closed values and every delay is within 5 to 20 s; `ToWaves()` returns them and throws for no wave or a missing enemy.
- **3a-AC13, named as a provisional design target:** from the three prototype assets, the three checks of the criterion; the 2c-AC9 tests pass on a one-enemy wave.
- **3a-AC14:** two battles with the same waves and the same placements at the same ticks are identical after every tick of a whole run. The Core asmdef and source tests pass.
- **3a-AC15:** `Is.Not.AllocatingGCMemory` over several hundred ticks that cover spawns, kills, exits and lost lives, after a warm-up. Put the largest wave first, so the warm-up reaches the peak.

**PlayMode**, in `Prototype.unity`, driven tick by tick as in 2c:
- **3a-AC8, AC9, at start:** no enemy view is active, 32 inactive objects named `Enemy` exist, the labels read "Wave 0/10", "Lives 10" and "Next wave in 15 s", and `end-screen` is hidden.
- **3a-AC8:** the wave label follows each wave's first enemy; the countdown is hidden while a wave spawns, reads "Next wave in 8 s" right after its last enemy and is hidden from wave 10 on; the lives label drops at each exit.
- **3a-AC9:** with one tower and several enemies on the board, the active enemy views equal `Board.EnemyCount`, each on its enemy's position with a fill of `Health / MaxHealth`. Over a whole run with the eight towers of 3a-AC13, no object named `Enemy` or `Projectile` is created or destroyed. 2c-AC11 passes on the first enemy.
- **3a-AC5, AC6, lost:** with no tower the end screen shows "Defeat"; over the next frames at `timeScale` 1 no enemy view moves, a click on a free cell places nothing and the hover is inactive.
- **3a-AC5, AC6, won:** with the eight towers the end screen shows "Victory" and no projectile view is active.
- **3a-AC7:** send the button a UI Toolkit click, not a call to `Restart()`. After the reload there is no tower object and no tower on the board, the lives are full, the labels are those of the start and the end screen is hidden. Do it twice in one test.
- **3a-AC11, 2a-AC4:** the 2b PlayMode tests and the material, shader and no-collider tests pass with the new assets.

**Orchestrator in the browser, then Eduardo (3a-AC16 and what no test can judge)**, at `http://localhost:8766` after a reload without cache, in Chrome and Safari:
- The 15 s before wave 1 are enough to place a few towers; the countdown is easy to read and the HUD never covers the placement message.
- Enemies of one wave are told apart, each with its own bar, and move without stutter in wave 10.
- Building nothing ends in "Defeat" in under a minute; a handful of towers along the route ends in "Victory" in under three.
- The end screen is readable over the frozen board, and a mouse click on Restart gives an empty board with no tower under the button.
- Restart works three times in a row with no slowdown.
- 2b and 2c still work: place, remove, the four messages, the hover, the range disc and the projectiles.
- The browser console shows no error, and `http://localhost:8765` still shows the 2c build.

## Risks
- **Mouse on the button.** The scene has no `EventSystem`. UI Toolkit in Unity 6 should take pointer input from the Input System package without one, but nothing proves it yet, and the PlayMode test sends a synthetic click. T3a-7 checks a real click in Play; if it does not arrive, add an `EventSystem` with the Input System UI module to the scene there.
- **Test churn.** The `Battle` constructor and the enemy API change under 2c's suite, and most PlayMode tests assumed an enemy at start-up. T3a-6 lists the changes; Stage 5 reruns everything.
- **Design-target numbers.** 3a-AC13 comes from a rough port of Core, not from Core. If a check fails by a small margin, the criterion is corrected, not the code.
- **WebGL performance.** A tick is at most about 20 enemy steps and one range scan per idle tower, a few thousand distance tests with a hundred towers. About 60 enemy renderers at the peak stay on the SRP Batcher. GPU instancing stays deferred (ADR 0004); the browser check is the measurement.
- **GC.** An enemy object or view is created only at a new peak, a HUD string about once a second, and a restart drops the whole scene at once.
- **Placement during a wave.** With a stream of enemies on the route, "An enemy is in the way" shows far more often than in 2c. That is the closed rule; watch how it feels.
- **Wrong build in the browser.** Two servers run side by side; T3a-8 makes the script refuse to pass one off as the other.
- **Coupling.** `RunHudView` and `CombatView` read `Battle` directly, and `Restart()` ties the bootstrap to the scene. Both are deliberate (ADR 0004).
