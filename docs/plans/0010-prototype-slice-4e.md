# Technical plan 0010 – Prototype slice 4, step 4e

Waves of mixed elements. Architecture: `docs/adr/0004-run-waves-outcome-restart.md`, amended for the walk over the groups. Numbers: `docs/balance/0001-prototype-economy.md` ("the balance document"), re-proposed for this step. Looks: `docs/art/visual-direction.md` ("the visual direction"), section 1.3. Prototype mode: this plan carries the acceptance criteria. It builds on plan 0009 and reuses "Editor sequencing" of plan 0007.

**What Eduardo sees:** a wave announced as `Light + Shadow`, its enemies of both colours in one stream, and towers of both elements needed at once: a tower wastes its shots on the immune enemies in front while the ones it could hurt walk by behind them.

## The rule and its two pins
**The rule.** The groups of a wave enter one after another, in the wave's order. The first enemy of a wave enters when the wave's delay ends; every other enemy enters its own group's spawn interval after the enemy before it.

Both pins hold in Core and in the port, on the ticks stated (`Tick()` calls, the first is 1). Enemies: speed 2, health 45, reward 10.

**Schedule pin.** A schedule of two waves, stepped alone.
- Wave 1: delay 1 s; group 1 is 2 Light enemies with interval 1 s; group 2 is 2 Shadow enemies with interval 0.5 s. Wave 2: delay 2 s; one group of 1 Light enemy.

| Tick | Enters | `StartedWaves` | `EnteredWaves` | `SecondsToNextWave` |
|---|---|---|---|---|
| 61 | Light, wave 1 | 1 | 0 | 0 |
| 121 | Light, wave 1 | 1 | 0 | 0 |
| 151 | Shadow, wave 1 | 1 | 0 | 0 |
| 181 | Shadow, wave 1 | 1 | 1 | 2 less one tick |
| 301 | Light, wave 2 | 2 | 2 | 0, finished |

Nothing enters on any other tick, and `SecondsToNextWave` is 0 on every tick from 61 to 180.

**Mixed-fire pin.** The prototype map, 10 lives, 40 gold, the chart and the Light tower of the balance document (cost 40, range 2.5, damage 10, 1.5 shots/s, projectile 10). One wave: delay 0, bonus 7; group 1 is 1 Light enemy; group 2 is 1 Shadow enemy with interval 1 s. Before the first tick a Light tower is bought on (2,3), which leaves the route as it is.

| Tick | What happens |
|---|---|
| 1 | The Light enemy enters; the tower launches at it. |
| 12, 49, 95 | The shots of ticks 1, 40 and 80 land on the Light enemy: health 45, no hit record, its immunity mark is that tick. |
| 61 | The Shadow enemy enters. |
| 80 | The tower launches at the Light enemy at (2.18, 5.18), with the Shadow enemy in its range at (0.67, 4.00). |
| 120 | The Light enemy is out of range; the tower launches at the Shadow enemy at (1.71, 4.71). |
| 131 | That shot lands at (1.97, 4.97): Shadow health 45 to 25; the one hit record of the run, 20 and strong. |
| 652 | The Light enemy exits: 9 lives. |
| 712 | The Shadow enemy exits with health 25: 8 lives, the bonus is paid, the run is won with 7 gold. |

Four launches in all, three at the immune enemy, one of those with an enemy it could hurt in range; no kill.

## Approach
A mixed wave is a wave with several groups, which the data format, the bonus, the wave index of an enemy and both HUD labels already take (plan 0007). What is missing is the walk: `WaveSchedule` reads group 0 and refuses any other number of groups. This step makes it walk the groups with the one timer it has, and that is all the production code. `Battle`, `RunDefinition`'s fields, every view, `Hud.uss` and the scene do not change.

No gap between groups is added. The group's own interval is the time before each of its enemies, so a block wave and an alternating stream (groups of one) are both plain content, a wave of one group behaves as before, and no field is ever dead or contradicts the wave's delay. At a cadence of 1 s the last enemy of one group and the first of the next are 2 cells apart, inside one tower's range, which is what makes both elements matter at once.

Discarded: a gap of its own on the wave or on the group (one more field whose only new use, a pause, pulls the two elements out of one tower's range; if content proves it needs one, it is one field on the group); groups that run at the same time, each with an offset (a timer per group and several enemies per tick); enemies of two elements inside one group (a group is one enemy asset).

## Acceptance criteria
- **4e-AC1 Groups in order.** The rule above. At most one enemy enters per tick, also across a change of group, and the cadence does not drift. `WaveSchedule` refuses only a wave with no group. A wave of one group enters as before: 3a-AC1 holds, and the spawn interval of a wave's first group does not delay its first enemy. This replaces the last sentence of 4a-AC1.
- **4e-AC2 A wave stays the unit.** `StartedWaves` rises with the first enemy of a wave's first group and `EnteredWaves` with the last enemy of its last group. `SecondsToNextWave` is above 0 only while a wave's delay runs, never between two groups, and the next wave's delay counts from the last enemy of the last group. Every enemy carries the index of its wave, whatever its group. So 4a-AC2 and AC3 hold as written: one bonus per wave, also when the board is empty between two of its groups.
- **4e-AC3 Mixed fire.** With enemies of several elements in a tower's range, targeting, damage, the hit log and the immunity mark are those of 4b-AC2 to AC5 and 4d-AC3 and AC4: the mixed-fire pin.
- **4e-AC4 Announcement.** For a wave of several elements the countdown and the wave line are those of 4b-AC10: each distinct element once, in order of first appearance, joined by the visual direction's separator, each in its UI colour. Both labels lie inside the top bar and do not overlap.
- **4e-AC5 Data.** `Run_Prototype.asset` and the enemy assets equal the balance document, and at least one wave has groups of both elements. Every enemy asset under `Data/Assets/` is used by a group, is named `Enemy_<Element><Health>` and has an element that is in the chart and in the palette. This replaces the waves and the enemy assets of 4b-AC11.
- **4e-AC6 Design target.** 3b-AC9 holds with the runs of the balance document for this step. 4d-AC18(c) holds on the last wave of its reference order; if it does not, the visual direction is revised, not the code. This replaces 4b-AC12.
- **4e-AC7 Core, determinism and allocation.** The walk is a rule of `ElementalTD.Core`. 3b-AC10 holds on a run with mixed waves, and 3b-AC11 on the ticks where the group changes.
- **4e-AC8 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari. Everything of 2b to 4d that this plan does not replace still holds.

4e-AC3 and AC4 are met by code that exists: they have tests and no task.

## Tasks
Order and editor use: plan 0007, "Editor sequencing", with `tools/unity-lock.sh` around every batch run. T4e-1 and the tests start once the 4d test pass is committed; T4e-2 runs in parallel from now.

### T4e-1 – Core: the walk over the groups
Serves 4e-AC1, AC2 and AC7. Code only; `WaveSchedule.cs`, and the comments that state the old meaning in `WaveGroup.cs` and `RunDefinition.cs` (the tooltip of `spawnIntervalSeconds`).

```csharp
// WaveSchedule: private int _group; private int _enteredInGroup;      // replace _enteredInWave
// Constructor: refuses a wave whose GroupCount is below 1.
// Step, once the timer has run out and the remainder is capped:
//   group = _waves[_wave].GroupAt(_group); enemy = group.Enemy
//   StartedWaves++ when _group == 0 && _enteredInGroup == 0
//   more enemies in the group -> _timer += group.SpawnIntervalSeconds
//   else a next group         -> _group++; _enteredInGroup = 0; _timer += that group's SpawnIntervalSeconds
//   else                      -> the next wave's delay or IsFinished, as today, with _group = 0
```

**Done when:** no other production file changes; Core has no `UnityEngine`; `Step` has no `new`; the EditMode suite of 4d passes except the two tests under "Tests that change" that expect the refusal.

### T4e-2 – Balance document and port
Serves 4e-AC5 and AC6. `balance-designer`, chained by the orchestrator.

**Defaults,** broken only with a stated reason:
- Waves 1 to 3 are unchanged.
- Each wave keeps its total count and the 1 s cadence, so every entry tick of the run and the building-nothing pins hold.
- Only which waves from 4 on are mixed, their groups (order and split) and the health change. Gold and bonuses change only if the simulation demands it.

**The port** (`tools/balance/`) follows the rules for the port of plan 0007 and these:

11. A wave is a delay, a bonus and its groups in order; a group is an enemy (speed, health, reward, element), a count and an interval. Today's one-group wave still works, so `verify3c.py`, rules 5 and 10 and the 4b reference order (`ref4_w8_k2.json`, won on tick 10165) reproduce.
12. The rule above, on the one timer: after an enemy enters, the timer gains the interval of the group the next enemy belongs to, or the next wave's delay after the wave's last enemy. The remainder and its cap are unchanged.
13. A wave has entered when the last enemy of its last group has, and its bonus waits for that.
14. The announced elements of a wave are the distinct elements of its groups, in order.
15. Both pins above.

To extend: `core.py` (`Waves.step` and `next_index`; the bonus check reads the count of one group), `econ.py` (`mk_waves`; `wave_elem` and the reading bots take one element per wave), `p4.py` (`params4`), `proposal.py` (`ORDER`), `refrepair.py` (the swap sells towers of the one element that cannot hurt the wave), and a report script for this step.

**The document gives** everything the tests pin: per wave its delay, bonus and groups (element, count, interval, health, enemy asset); the enemy assets; building nothing; a reference order that wins (ticks, cells, towers, outcome, lives, gold left, first kill); the share of launches wasted while an enemy the tower could hurt was in its range; and the share of ticks with both elements on the board.

**Done when:** the orchestrator has validated the document.

### T4e-3 – Assets
Serves 4e-AC5. Editor open on the worktree; after T4e-1 and T4e-2. Through `unity command`: `Run_Prototype.asset` with the document's waves and groups; one enemy asset per element and health the document names, none left that no group uses. No scene, prefab, material or USS change.

**Done when:** clean compile with an empty console; in Play the countdown before the first mixed wave names both elements and enemies of both colours enter in the document's order; everything is saved and `.vscode/settings.json` is reverted.

### T4e-4 – WebGL build
Serves 4e-AC8. As T3b-5 of plan 0005.

## Tests for the qa-tester
On the worktree with its editor closed. A test not listed under "Tests that change" that fails is reported before it is changed. No test of this step pins a value of `DamageNumberView` or of `Hud.uss`.

**Tests that change**
- `WaveBonusTests` (the schedule refuses two groups) and `RunDefinitionTests` (the schedule refuses a converted wave of two): the schedule now accepts both; a default wave is still refused.
- `RealAssetRunTests`: the wave table becomes one row per group; the count of enemy assets becomes "every enemy asset is used by a group"; the reference order and its pins take the document's values.
- `SceneTestBase.ReferenceOrder` and the scene tests that replay it, the 4d-AC18(c) test included: the document's values.
- `RunSceneTests`: any text that names the elements of a wave the document changes.

**EditMode**
- **4e-AC1:**
  - The schedule pin's entry ticks.
  - A wave whose first group has an interval of 5 s still enters as its delay ends.
  - Four groups of one enemy with intervals 1, 0.5, 2 and 1 s enter 0.5, 2 and 1 s apart.
  - Each enemy has the stats of its own group.
  - Groups with an interval below one tick give exactly one enemy per tick across the change of group, and the next wave's delay is not eaten into.
  - A hundred groups of one enemy 0.25 s apart do not drift.
  - `WaveScheduleTests` passes untouched.
- **4e-AC2:**
  - The schedule pin's counters on every tick.
  - On `Battle`: the enemies of both groups carry the wave's index; a wave whose first group is dead before its second enters pays nothing until the last enemy of the second has left, then once; two mixed waves that overlap each pay once.
- **4e-AC3:** the mixed-fire pin, tick by tick, on the real map and tower.
- **4e-AC5, AC6:** as the 4b-AC11 and AC12 tests, on the document.
- **4e-AC7:** the determinism test runs the real run; `TickAllocation.AssertNoneAcross` over the ticks of a wave of three groups.

**PlayMode**, in `Prototype.unity`
- **4e-AC4:** on the frames the countdown runs before the first mixed wave, `next-wave-label` reads the visual direction's text with every name in its colour; once that wave starts, `wave-label` does. On those frames both labels' rectangles lie inside `top-bar` and do not intersect.
- **4e-AC3:** in the first mixed wave, with a tower of its first group's element beside the route: every active enemy view has the material of the enemy it shows, on every frame; an enemy of the first group carries the immune word while an enemy of the other element is on the board.
- **4e-AC8:** the scene tests of 2b to 4d pass with the changes above.

**Orchestrator in the browser, then Eduardo**
- The announcement shows both names in their colours, early enough to buy for both, and fits on the browser canvas at its narrowest.
- In the stream the two elements are told apart at a glance, also where one group ends and the next begins.
- **Towers shooting the immune enemies in front.** Look for:
  - the word "immune" under every enemy that takes those shots, within a second, for as long as they come;
  - the enemies behind them, of the other element, walking through that tower's range untouched: is it clear from the screen why?
  - the tower turning to them the moment the immune ones leave its range or die: does that read as the rule, or as a tower that woke up late?
  - whether the answer is obvious: towers of both elements, and which one first on the route.
  
  If it reads as broken towers, that is a design signal (the talent, or smarter targeting from the Parking lot), not a bug.
- Gold numbers over one enemy and "immune" under its neighbour stay readable on the vertical stretches.
- The run plays as the document says, a run with no tower still ends in "Defeat", and the browser console shows no error.

## Risks
- **Wasted shots become avoidable.** Until now no tower ever shot an immune enemy while one it could hurt was in range; in a mixed wave the leading group shields the one behind it. Health sized for 4b leaks: the document must be simulated with the rule.
- **Words and numbers side by side.** A word under most enemies of one group next to stacks of numbers over the other makes the visual direction's known limit (a full stack over the next enemy's word on a vertical stretch) common. Nothing new is built for it; the art-director judges it in the build. The word pool grows with the enemies on the board, and the hits per second fall, so the cap of numbers and the hit log hold.
- **Two implementations.** The port and Core meet at the two pins. The port applies the multiplier at launch and Core on landing; they agree while an element never changes in flight.
- **Re-pinned tests.** A new reference order moves every test that replays it. The defaults of T4e-2 keep the entry ticks, the building-nothing pins and wave 1, which the 4d scene tests build on.
- **Top bar width.** Two names on both sides fit at 960 by estimate, not by measurement; the PlayMode test and the browser check decide.
- **Shared worktree.** `SceneTestBase.cs` and `RunSceneTests.cs` change in this step and in the 4d test pass.
- **WebGL performance, GC and coupling.** Nothing added: one struct copy on a tick an enemy enters, no per-frame work, no new dependency.
