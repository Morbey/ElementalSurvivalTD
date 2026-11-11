# Technical plan 0006 – Prototype slice 3, step 3c

Enemies move in eight directions without cutting corners, and the prototype numbers are retuned for the shorter routes. Architecture: `docs/adr/0001-folder-structure-asmdefs-layers.md`, whose pathfinding paragraph is amended for this step. Numbers: `docs/balance/0001-prototype-economy.md` ("the balance document"), re-proposed in T3c-2. Prototype mode: this plan carries the acceptance criteria.

## Approach
The flow field stays one whole-number array computed from the exit. It now looks at eight neighbours with two costs: 408 for a straight step and 577 for a diagonal one. 577/408 is √2 to two parts in a million, and on any board of up to 408 cells these whole numbers order two paths exactly as their true lengths do, so "a shortest path" is literally true and no float takes part in the distances or in the choice of a step: the route is the same in the editor and in the browser. With two costs the search is no longer breadth-first: the same queue is kept and a cell re-enters it when its distance improves. The distances do not depend on the visiting order. On the 16 x 9 grid one recompute is at most about 170 cells taken from the queue and 1,350 neighbour checks (144 and 576 today), an estimated ten microseconds in the browser, once per purchase or sale and twice for a purchase refused for the path; never per tick.

A diagonal step is legal only when the two cells it passes between are free. A legal diagonal can therefore always be walked as two straight steps, so **which cells reach the exit is exactly what it was with four directions**: every `WouldBlockPath` answer stays as it is, and only distances and routes change. An enemy takes, among the legal steps that lie on a shortest path, the one with the smallest turn from its last step. `PathFollower` gives a diagonal step the length 577/408, so the speed along the path is the same in every direction. An enemy occupies the cells of its current step, which on a diagonal are four; that is the only change to the placement rule. Views already draw between tick positions, so Gameplay, Presentation and Data need no code change.

Discarded: free-angle paths (closed decision); float costs with √2 (a last-bit difference between Mono and IL2CPP can turn a tie into another route); costs 10 and 14 (five diagonals tie with seven straights that are 0.07 cells shorter: the same code with wrong ties); a binary heap (more code than 144 cells need; revisit when a map passes a few thousand cells); occupation by the enemy's real position, or only until it has passed the corner (float geometry in the placement rule, and the enemy still overlaps a tower placed right behind the corner); re-routing an enemy in mid-step (a visible snap); a tie-break by compass direction (a second ordering to carry; left before right is one table); a new refusal reason or message; rotation, turn animation and corner smoothing (out of scope).

## Acceptance criteria
- **3c-AC1 Eight directions, shortest path.** Every step of an enemy goes to one of the eight neighbours of its cell and lies on a shortest path to the exit, a straight step counting 1 and a diagonal one √2 (408 and 577 in `FlowField`). On an open board the distance of a cell that is `a` and `b` cells from the exit on the two axes is `408 * (max - min) + 577 * min`. This replaces the four-neighbour path of AC3 (plan 0001).
- **3c-AC2 No corner cutting.** A diagonal step is taken only when both cells it passes between are free of walls and towers. So an enemy rounds the end of a wall or a tower in two straight steps, and walls or towers that touch only by a corner let nothing through. A cell reaches the exit exactly when it did with four directions.
- **3c-AC3 Tie-break.** Among the legal steps on a shortest path an enemy takes the first of: straight ahead, 45° left, 45° right, 90° left, 90° right, 135° left, 135° right, back. Left is the turn from +x towards +y. So it goes back only when nothing else is as short. An enemy that enters has no last step and counts as heading +x. This replaces 2c-AC16.
- **3c-AC4 Speed.** An enemy covers its speed in cells per second along its path in every direction: a diagonal step takes 577/408 times as long as a straight one. `DistanceToExit` is the rest of that path in cells and falls by speed times the tick on every tick, across steps of both kinds; targeting (2c-AC2) reads it unchanged. On the empty prototype map the route is 9 straight and 9 diagonal steps, 8865 or 21.73 cells, and an enemy of speed 2 leaves on its 652nd tick, give or take one.
- **3c-AC5 Placement.** An enemy occupies the cells of its current step: the cell it left, the cell it heads for and, on a diagonal step, the two cells it passes between. A tower on any of them is refused as `EnemyInTheWay`, with the existing message. The rest of the placement rule is unchanged (2b-AC3, 3a-AC11, 3b-AC2). Whatever is bought or sold, no enemy is ever in a blocked cell, on a diagonal step beside a blocked cell, or without a path (2b-AC5).
- **3c-AC6 On screen.** With no change to any view, an enemy on a diagonal stretch moves on every frame by its speed times the frame time, as on a straight one (2c-AC11). Its health bar and the shots aimed at it follow it.
- **3c-AC7 Data.** 3b-AC8 holds with the values of the re-proposed balance document.
- **3c-AC8 Design target.** 3b-AC9 holds with the runs of the re-proposed balance document. 2c-AC9 holds while the document leaves the combat stats of `Tower_Prototype` and the speed and health of `Enemy_Prototype` alone.
- **3c-AC9 Core and determinism.** `FlowField` uses whole numbers only. Core still compiles with `noEngineReferences: true`, and no code file under `Gameplay/`, `Presentation/` or `Data/` changes. The same purchases and sales on the same ticks give the same positions, gold, lives and outcome after every tick (3a-AC14, 3b-AC10).
- **3c-AC10 Allocation.** A recompute of the field, the choice of a step and `Battle.Tick()` with enemies on diagonal steps allocate 0 bytes after warm-up (2b-AC10, 3a-AC15, 3b-AC11).
- **3c-AC11 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari. Everything of 2b, 2c, 3a and 3b that this plan does not replace still holds.

## Editor sequencing
As in plan 0004 and `docs/unity-cli-notes.md`, with the orchestrator's batch lock of plan 0005.

| Order | Work | Editor on the worktree |
|---|---|---|
| 1 | T3c-1 (`unity-dev`) and T3c-2 (`balance-designer`) in parallel: the port works from the rules of this plan, not from the C# | Closed. One batch EditMode run as the compile check: the tests under "Tests that change" fail until Stage 5 rewrites them, and no other may. |
| 2 | Stage 4 review of T3c-1, which changes the pathfinding of ADR 0001. The `qa-tester` writes the EditMode tests of 3c-AC1 to AC5, AC9 and AC10, which need no asset | Closed. |
| 3 | T3c-3, once the document is validated; skipped if no asset value changes | Open, then quit with a save. |
| 4 | Stage 5 on both suites | Closed. |
| 5 | T3c-4 and the browser check | Closed. |

A build right after order 1 already shows the movement, on the 3b numbers, which no longer balance. It is the orchestrator's call whether Eduardo gets that early look.

## Tasks

### T3c-1 – Core: eight directions
Serves 3c-AC1 to AC5, AC9 and AC10. Code only: `FlowField.cs`, `PathFollower.cs`, `Enemy.cs`, `Board.cs`.

```csharp
public sealed class FlowField
{
    public const int StraightCost = 408;   // Distance / StraightCost is the distance in cells
    public const int DiagonalCost = 577;   // 577/408 stands for the square root of 2
    // The eight steps, counter-clockwise from +x:  (1,0) (1,1) (0,1) (-1,1) (-1,0) (-1,-1) (0,-1) (1,-1)
    // Turns from the heading, in order of preference: 0, +1, -1, +2, -2, +3, -3, +4  (modulo 8)
    public int Distance(GridPos pos);      // in cost units; Unreachable as today
    // Compute, IsReachable and both TryGetNext keep their signatures.
}

public sealed class PathFollower
{
    public float StepLength { get; }       // 1 on a straight step and when standing, DiagonalCost / (float)StraightCost on a diagonal one
    public float Progress { get; }         // cells covered on the current step: 0 up to StepLength
    public float DistanceToExit { get; }   // Distance(To) / (float)StraightCost + (StepLength - Progress); 0 on arrival
}
```

- A step from a cell is legal when the target is on the grid and free and, for a diagonal, both cells that share a side with the cell and with the target are free.
- `Compute`: the exit gets 0 and enters the queue. A cell taken from the queue offers each neighbour it has a legal step with its own distance plus the cost of that step; a neighbour that improves takes the new distance and enters the queue unless it is already in it. The queue is a ring over the existing array, with one more `bool[]` allocated in the constructor.
- `TryGetNext(from, cameFrom, out next)`: the heading is `from - cameFrom`, or +x when they are equal. It returns the first step in the order of turns that is legal and has `Distance(target) + cost == Distance(from)`; false at the exit and on an unreachable cell, as today.
- `PathFollower.Step`: `Progress` grows by `dt * speed`; while it is at least `StepLength`, it drops by `StepLength` and the follower moves on, taking the length of the new step. `Restart` sets the length of the first step.
- `Enemy.SyncPosition` interpolates by `Progress / StepLength`.
- `Board.IsUnderEnemy`: true when the cell is within the rectangle spanned by `From` and `To`, which is two cells on a straight step and four on a diagonal one. `EveryoneReachesExit` does not change.
- Comments that speak of four neighbours, breadth-first or the order +x, -x, +y, -y are rewritten.

**Done when:** Core has no `UnityEngine`; `FlowField.cs` has no `float` or `double`; nothing outside `Core/` changes; in the batch EditMode run only tests listed under "Tests that change" fail.

### T3c-2 – Balance document
Serves 3c-AC7 and AC8. `balance-designer`, in parallel with T3c-1.

- Bring the Python port to the rules of T3c-1, the step length in float32 like the rest. It must reproduce the route, the distance and the 652 ticks of 3c-AC4.
- Re-propose the document on that port with everything the tests pin: the values per asset, the number of towers the starting gold buys, the outcome and tick of building nothing, and a reference build order that wins, with its ticks, cells, outcome, lives, gold left and first kill. It says whether the stats of 2c-AC9 change. The map layout stays.
- Starting point: on the 3b numbers, building nothing now loses near tick 2931 (3089 before) and the 3b reference order loses.

**Done when:** the orchestrator has validated the document.

### T3c-3 – Assets
Serves 3c-AC7. Editor open on the worktree; after T3c-1 and T3c-2. Through `unity command`: the document's values in `Run_Prototype.asset`, `Tower_Prototype.asset` and the enemy assets; one new `Data/Assets/Enemy_<Name>.asset` per enemy definition the document adds, and the ones no wave uses any more deleted with their `.meta`.

**Done when:** clean compile with an empty console; in Play the enemies take the diagonal route of 3c-AC4 and a run starts with the document's gold; everything is saved and `.vscode/settings.json` is reverted.

### T3c-4 – WebGL build
Serves 3c-AC11. As T3b-5 of plan 0005.

## Tests for the qa-tester
On the worktree with its editor closed. Positions below are cells of the prototype layout unless a board is named; "open" boards have no wall.

**Tests that change**, because they pin four directions
- `FlowFieldTests`: the Manhattan test becomes the open-board formula of 3c-AC1; the spawn distance 27 becomes 8865; the S-shape route becomes (0,4) (1,4) (2,5) (3,6) (4,7) (5,7) (6,7) (7,6) (8,5) (9,4) (9,3) (9,2) (9,1) (10,1) (11,1) (12,1) (13,2) (14,3) (15,4), every step legal by 3c-AC2; the fixed-order tie-break test goes.
- `NoReversalTests` goes; the 3c-AC3 cases replace it.
- `PathFollowerTests`: the full run takes 10.86 s, not 13.5.
- `BoardTests`: a tower on (5,7) gives the spawn 9442, not 29; in the refusal cases the follower is on (1,4) heading for (2,5); the fuzz budgets its steps in cells (`Distance / StraightCost`). `MapDefinitionTests`: the two hand-counted distances are counted again in the new costs.
- `RealAssetRunTests`, `SceneTestBase.ReferenceOrder` and the `RunSceneTests` that replay it: the document's new values, ticks, build order and final gold, as in plan 0005. HUD texts follow the document.
- `PrototypeSceneTests`: with a tower on (5,7) the distance is 9442 and the enemy passes (5,8) and then (9,1), never (5,7); the 2c-AC11 test measures on the straight stretch (9,4) to (9,1), which lasts 1.5 s.
- Every other test passes untouched. One that does not is reported before it is changed.

**EditMode**
- **3c-AC1:** the formula on an open 7 x 5 with the exit on (3,2), for every cell. On 200 seeded random 16 x 9 boards, `Distance / 408.0` is within 0.001 of a reference search written in the test with `double` and √2, and the same cells are unreachable. Following `TryGetNext` from any reachable cell, the costs of the steps add up to its distance.
- **3c-AC2:** on a 3 x 2 with a wall on (1,0), (0,0) to the exit on (2,0) costs 1632 through (0,1), (1,1) and (2,1); the same with a tower in place of the wall. On an open 4 x 4, spawn (0,0), exit (3,3): towers on (0,3), (1,2) and (2,1) are placed and the spawn distance is 2448 through (3,0); a tower on (3,0) is then `WouldBlockPath`. On the prototype layout, towers on (4,8) and (5,7), or on (5,7) and (6,8), touch by a corner: the second one is `WouldBlockPath`. An L-shaped gap: on a 3 x 3 with walls on (1,0) and (2,1), (0,0) reaches the exit on (2,2) by (0,1) and (1,2), at 1393. On the 200 random boards, a cell is reachable exactly when a four-neighbour flood fill written in the test reaches it.
- **3c-AC3**, on an open 5 x 5, as `TryGetNext(from, cameFrom)`:

  | Case | Exit | From, came from | Next |
  |---|---|---|---|
  | Straight ahead on a tie | (4,1) | (1,0), (0,0) | (2,0) |
  | A diagonal heading also goes on | (4,3) | (1,1), (0,0) | (2,2) |
  | The smaller turn | (4,3) | (2,2), (2,1) | (3,3) |
  | Left before right, heading +x, tower on (2,2) | (4,2) | (1,2), (0,2) | (1,3) |
  | Left before right, heading -x, tower on (2,2) | (0,2) | (3,2), (4,2) | (3,1) |
  | A turn rather than back | (4,3) | (2,2), (3,3) | (3,2) |
  | Back when strictly shortest | (2,0) | (2,2), (2,1) | (2,1) |
  | No last step, tower on (2,2) | (4,2) | (0,2) | (1,3) |
  | No last step | (4,1) | (0,0) | (1,0) |

  A follower walking from (0,0) to the exit on (4,1) passes (1,0), (2,0) and (3,0). On the two-corridor board of `BoardTests`, a follower on (2,4) heading for (3,4) with a tower then placed on (4,4) turns back on (3,4).
- **3c-AC4:** on an open 5 x 5 from (0,0) to (4,4), after 60 ticks at speed 2 the enemy is on x = y = 1.414, within 0.001, and it arrives after 2.83 s. Over a whole crossing of the prototype map, `DistanceToExit` starts at 21.728 and falls by `speed * TickSeconds` on every tick, within 0.0001, and the enemy leaves on tick 652, give or take one. With two enemies on different kinds of step, the tower shoots the one with less path left.
- **3c-AC5:** on an open 5 x 5, spawn (0,0), exit (4,4), with an enemy on the step (1,1) to (2,2): a tower on (1,1), (2,2), (2,1) or (1,2) is `EnemyInTheWay` and changes nothing; one on (3,2) is placed, and the enemy then goes (2,3), (3,4), (4,4). Beside a straight step both side cells can be built on. The seeded fuzz of 2b-AC5 passes with one more check after every action: on a diagonal step, neither cell the enemy passes between is blocked.
- **3c-AC7, AC8:** as the 3b-AC8 and AC9 tests, on the new document.
- **3c-AC9:** a source test: `FlowField.cs` contains neither `float` nor `double`. The determinism tests of 3a-AC14 and 3b-AC10 pass on the prototype layout, whose route now has diagonals; a field computed twice, and after a tower is placed and removed, is equal cell by cell.
- **3c-AC10:** the existing allocation tests pass on routes with diagonals; `Compute` and `TryGetNext` with a heading are inside `Is.Not.AllocatingGCMemory`.

**PlayMode**, in `Prototype.unity`
- **3c-AC6:** from the tick an enemy is on (1,4) heading for (2,5), at `timeScale` 1, its view moves on every frame by `speed * Time.deltaTime` within the tolerance of the 2c-AC11 test, and equally on x and z, for 40 frames. A projectile view in flight at a moving enemy and the health bar stay with it.
- **3c-AC5:** with the first enemy on that diagonal step, a click on (2,4) shows "An enemy is in the way." and places no tower.
- **3c-AC11:** the scene tests of 2b to 3b pass with the changes listed above.

**Orchestrator in the browser, then Eduardo**, at `http://localhost:8766` after a reload without cache, in Chrome and Safari:
- With no tower, enemies cut the open ground diagonally and round both wall ends in two straight steps, never across a corner.
- Two towers that touch by a corner stop them like a wall; a tower beside a diagonal lane turns the lane into an L.
- Speed looks the same on straight and diagonal stretches, with no jump at a turn.
- During a wave, how often does "An enemy is in the way." refuse a cell beside a diagonal lane? Is it understandable?
- Do the doglegs on open ground (straight, then diagonal) read as a path or as a glitch?
- The run plays as the new document says: building nothing loses, a sensible build wins. Gold, selling, the hover, the range disc and the end screen work as in 3b, and the browser console shows no error.

## Risks
- **More refusals beside a diagonal lane.** The two cells beside a diagonal step are where the player builds to close it, and with enemies one second apart at speed 2 such a cell is occupied about 70 percent of a wave. Nothing shorter keeps enemies from clipping a tower placed in mid-step. If it annoys in the browser, the fix is design (a queued placement), not a narrower rule.
- **The balance port and Core are two implementations.** T3c-2 runs before the C# exists; the pins of 3c-AC4 tie both to this plan. If a 3c-AC8 check fails by a small margin, the criterion is corrected, not the code.
- **Mazes and open ground diverge.** A corridor one cell wide is as long as before, because its turns cannot be cut; only open ground gets shorter (about 20 percent on the empty prototype map). The retune moves value towards mazing.
- **`Distance` changes unit.** It is now 408 per cell. Only `PathFollower` reads it in the game; tests that compared it with cell counts are listed above.
- **Floats.** The step length is one constant and the walk only adds, subtracts and divides, so the last-bit note of ADR 0003 stands unchanged; the route itself no longer depends on any float.
- **WebGL performance and GC.** The recompute costs about three times today's, on a click. Per tick an enemy gains one division; the choice of a step runs once per cell crossed, at most 24 cell reads. The direction tables are static and allocated once.
- **Board size.** The exact ordering holds up to 408 cells; beyond that an enemy could take a path longer by under a thousandth of a cell. Beyond a few thousand cells the queue should become a heap.
- **Scene tests on real numbers**, as in plan 0005: a rebalance can break them without a code change.
