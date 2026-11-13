# Technical plan 0009 – Prototype slice 4, step 4d

Damage numbers stacked over each enemy, the word "immune" over an enemy that towers waste shots on, and the placement refusal at the refused cell. Architecture: `docs/adr/0006-hit-log-text-over-the-board.md`, with `docs/adr/0005-elements-chart-damage-rule.md` for the kind of a hit. Looks: `docs/art/visual-direction.md` ("the visual direction"), sections 2 and 1.3. Numbers do not change: `docs/balance/0001-prototype-economy.md` stays as it is. Prototype mode: this plan carries the acceptance criteria. It builds on plan 0008 and reuses "Editor sequencing" of plan 0007.

**What Eduardo sees:** every hit shows its own gold number in a short column over its enemy: the newest nearest the enemy, the older ones stepping up to make way, all walking along with it; an enemy that a tower cannot hurt carries a grey "immune" for as long as the shots keep coming; and a refused click says why at the cell he clicked.

**Built and standing.** Core's tick count, kind of a hit, hit log and immunity mark; the pooled labels (`FloatingLabel`, `BoardToPanel`, `floating-texts` in `Hud.uxml`); the immune word; the refusal at the cell; their looks and scene values. That code is the reference. What is left: where a number sits and how it moves (T4d-6 and T4d-8), the build (T4d-5) and every test of the step.

## Approach
**Core records, views read.** `Battle` counts its ticks and keeps the last 64 hits in a fixed log: position, damage dealt, kind, tick, and now the entry number of the target. A view remembers how many hits it has read and reads the ones after, so a frame that ran five ticks sees all five ticks' hits and a tick calls nothing outside Core. The board numbers every entry of the run, so the number says which enemy was hit as a plain value: a view looks for it among the enemies on the board and either finds that enemy or finds none, also when the pooled object is already another enemy. A hit on an immune enemy adds no record: the enemy remembers the tick of the last one and the view asks each enemy on the board (ADR 0006).

**The numbers are the stack.** The view keeps nothing per enemy. Each number alive keeps its record's tick and entry number, where its enemy was last, its slot and its lift; the stack of an enemy is the numbers that carry its entry number, found by a scan of at most the cap. Where a number is drawn is a function of those values, the battle clock and where its enemy is drawn (the interpolation `CombatView` uses, computed in this view): every newer number lifts it by its own height over its own first moments, and the lifts add up. So the motion is the same whatever frames were drawn, a test ticks it by hand, and two numbers of one enemy cannot intersect however close the hits land. A simulation of this stack on the float32 port of Core (the reference order, last wave, one frame per tick) shows two numbers intersecting on 6.6 % of the frames, against 74 % for numbers that stand where the hit landed.

**Text over the board is UI Toolkit labels moved by transform.** A label is moved by `translate`, grown by `scale` and faded by `opacity`, never by layout; sizes, colours and outlines are USS classes; the height of a number is a scene field per kind, so no code measures a label; a text or a class is written only when the pooled label held another. Core answers four kinds; today's assets only produce strong and immune, so tests reach weak and normal through a chart of their own and through the view's `Show`.

Discarded: an enemy reference with its entry number in the record, as a shot holds (a view could read the object after it is reused; a number cannot be read wrong); a stack per enemy kept by the view (a map of enemies, and stacks that outlive their enemy); the stack in Core (a look in the rules); a push that restarts an ease from where the number is (it depends on the frames drawn unless replayed per tick, and in the simulation numbers of one enemy then intersect by up to 1.7 px on 1.5 % of the last wave's frames); one summed number per enemy (refused by Eduardo); and those of ADR 0006.

## Acceptance criteria
p is the direction's entry time. A number's age is `(TickCount − its tick + the fraction of a tick left over) × TickSeconds`. Its height is the direction's for its kind.

- **4d-AC1 Tick count.** `Battle.TickCount` is 0 before the first tick and rises by one for every `Tick()` that runs; a finished battle counts no more.
- **4d-AC2 Kind of a hit.** The kind is decided in Core from the one multiplier read on landing: immune at 0, weak below 1, normal at 1, strong above 1. No file under `Presentation/` or `Gameplay/` reads a multiplier.
- **4d-AC3 Hit log.** Every shot that lands on a target that is not immune adds one record, the killing hit included: the target's position in cells on that tick, the amount (the shot's damage times the multiplier: what `TakeDamage` receives, not cut to the health left), the kind, the tick and the target's entry number. `Enemy.Entry` numbers the entries of the run from 1 in the order the enemies enter, so an enemy on a reused object has a new one. A shot dropped because its target left adds no record. `HitCount` counts the records since the run began; the log answers for the last `HitLogCapacity` (64) of them, in landing order, and throws for any other index. Hits beyond that are overwritten oldest first; their damage is dealt all the same.
- **4d-AC4 Immunity mark.** A shot that lands on an immune target adds no record; the enemy's `ImmuneHitTick` becomes that tick. It is 0 from every entry until the first such shot, also on a reused object.
- **4d-AC5 Rules untouched.** Damage, targeting, gold, waves and the tick order do not change: every EditMode test of 4c passes untouched, 3b-AC10 holds, and `Tick()` allocates nothing, also past 64 hits.
- **4d-AC6 A number per hit.** From the first frame drawn after the tick of a record, one number shows for it. Its text is the amount rounded to the nearest whole number, a half up, and at least 1, written once per amount with the invariant culture; its look is its kind's. It enters at slot 0 of its target's stack: hung by its bottom centre the direction's height above the enemy, at scale `min(1, age / p)²` about that point. A frame that ran several ticks shows every hit of those ticks, each aged from its own tick; what the numbers show depends on the records, the battle clock and where the enemies are drawn, never on which frames were drawn.
- **4d-AC7 The push and the life.** Every newer number of the same enemy lifts a number by the newer one's height, eased out over the newer one's first p seconds, and the lifts add up: its lift, in panel pixels, is the sum over those newer numbers of `height × (1 − (1 − min(1, age / p))²)`. Nothing else moves a number against its enemy, and none moves down, also when a newer one ends first. With t its age over its kind's life, a number is fully opaque up to the direction's share, then fades linearly to 0, and is gone at t = 1.
- **4d-AC8 Budget and caps.** A number's slot is the count of newer numbers shown for its enemy. In the last slot of the direction's budget, and beyond it, its opacity is at most the direction's. The hit that takes it beyond the budget makes it leave: still lifted as any other, its opacity falls linearly to 0 over p from that hit's tick, and its label is then free. At most the direction's cap of numbers is alive in all: a hit takes a free label, and with none free the oldest number's. From the direction's cut-off alive, a weak hit shows no number and lifts none. The labels are made at start: none is created or destroyed afterwards.
- **4d-AC9 Immune word.** Each enemy on the board whose `ImmuneHitTick` is not 0 and younger than the direction's hold, in battle time, carries one word, however many towers hit it: the direction's text and look, hung by its top centre at the direction's place below the enemy as it is drawn, following it; fully opaque, then fading linearly over the direction's last part of the hold. An immune hit shows no number, and words do not count towards the cap. The word is gone on the first frame after its enemy has left the board.
- **4d-AC10 Refusal at the cell.** `PlacementRefused` carries the reason and the cell; every refusal has one. The reason's text, unchanged, shows in one label whose bottom centre starts the direction's height above the cell's centre, rises and fades as section 1.3 states, in unscaled seconds, and is kept the direction's margin inside the board area on every frame. A newer refusal restarts it at its own cell. The message line shows only the survived line: a refusal neither shows there nor replaces it, and both may be on screen together. This replaces where and how long a refusal shows in 2b-AC4, 4a-AC4 and 4b-AC18; the text per reason holds, and so does the rest of 4a-AC4 and 4b-AC18 for the survived line.
- **4d-AC11 End of a run.** From the first frame after the run is won or lost, no number, word or refusal shows. After a restart all three work again.
- **4d-AC12 Clicks.** No floating text takes picking: a hover or a click on a cell under one does what it does without it (4b-AC8).
- **4d-AC13 Layers and cost.** Core still compiles with `noEngineReferences: true`; the views hold no rule, number, colour or text of their own. With the cap of numbers alive and a word on every enemy, both views allocate 0 bytes per frame after warm-up, and a tick allocates nothing (4d-AC5). A text is built only the first time an amount occurs. On a frame with no new hit and no refusal, no text and no class is written and no label's layout rectangle changes.
- **4d-AC14 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari, and passes the measurement under "Orchestrator in the browser". Everything of 2b to 4c that this plan does not replace still holds.
- **4d-AC15 Following the enemy.** While the enemy a record names is on the board, its numbers hang from it as it is drawn, between its last two ticks as its view is. From the first frame it is not, they stay at the board point where it was last, the position in its last record or where it was last drawn, whichever is later, also through a resize; they keep their lifts and finish their lives, and the number of a killing hit joins them there. An enemy that enters on a reused object carries none of the numbers of the enemy before it.
- **4d-AC16 The top line.** A number whose top (its bottom less its height) would lie above the direction's limit from the panel's top sits with its top on that line instead, moved right by the direction's step times its eased slot: its slot less, for each newer number still lifting it, the share of that lift not yet done. This holds on every frame of a push and after the enemy has left.
- **4d-AC18 Measured readability.** The direction's measured targets (section 2), each on a run drawn one frame per tick, two numbers overlapping when their rectangles intersect by more than half a panel pixel on both axes: (a) numbers of one enemy off the top line never overlap, with hits on one walking enemy from the same tick to many ticks apart; (b) with five towers on one enemy whose numbers are on the top line, the frames with an overlap are at most the direction's share of the frames that show a number; (c) in the last wave of the reference build order, the frames on which any two numbers overlap are at most the direction's share for numbers of different enemies.

The sizes, colours, outlines, heights, lives, times, budget, limits, caps and wording are those of the visual direction, section 2 and section 1.3 ("Placement refusal at the cell").

## What this step takes from the visual direction
| Section of the direction | Where the values live |
|---|---|
| 2: size, weight, colour and outline of weak, normal, strong and the immune word | `Hud.uss`, one class each |
| 2: per kind the life and the height; the height above the enemy, the entry time, the budget, the top slot's opacity, the share of life at full opacity, the top line and its step, the cap, the weak cut-off; the immune hold, its fade and the word | Fields of `DamageNumberView` in the scene |
| 1.3 refusal: size, weight, colour, outline | `Hud.uss` |
| 1.3 refusal: the height above the cell, the rise, the life, the fade, the margin inside the board area; the five texts | Fields of `RefusalView` in the scene |

Not in this plan: the critical look, which is designed in section 2 and is built with the rule for critical hits, as its own step with the flag on the record (no criterion here is numbered 4d-AC17); x0.25 content; gold pop-ups, a death effect and sound; health bars hidden until the first damage (Parking lot). Three holes in section 2 to answer when the critical look is built: on the top line a critical is wider than the line's step; a critical under a critical that holds at the top has no rule; and its pop covers the older numbers above it.

**Read of the direction, for the art-director to confirm in its section 2.**
1. The entry, the push and the fade of a number that leaves share one time, so an older number always rises at least as far as the new one grows.
2. The push is 4d-AC7: lifts that add up. It is the direction's push whenever hits on one enemy are at least p apart; closer than that, as about a fifth of them are in the late waves of the simulation, some on the same tick, it is the only form in which numbers of one enemy never intersect.
3. The budget counts slots, three numbers whatever their kinds; its pixel values are those of strong numbers.
4. On the top line a number stands the step times its slot to the right of its enemy, slot 0 over the enemy.
5. A number never moves down.

## Tasks
Order and editor use: plan 0007, "Editor sequencing", with `tools/unity-lock.sh` around every batch run (`docs/unity-cli-notes.md`). T4d-6 is code only with the editor closed; the stage 4 review follows; then T4d-8 with the editor open, stage 5 and T4d-5. No balance task.

### T4d-6 – Core and Presentation: the record names its enemy, the stack
Serves 4d-AC3, AC5 to AC8, AC13, AC15, AC16 and AC18. Code only.

```csharp
// Enemy:         public int Entry { get; private set; }     // its number among the entries of the run, from 1
// Hit:           public readonly int TargetEntry;            // Enemy.Entry of the enemy hit; the last constructor parameter
// FloatingLabel: public void SetScale(float scale);          // on the anchor, written only on a change
```

- `Board` counts the entries and `SpawnEnemy` gives the next number to `Enemy.Enter`; an enemy no longer counts its own. `Projectile` is untouched: it compares the same property. `Battle` writes `target.Entry` into the record.
- `DamageNumberView`, fields: `risePixels` and `spawnOffsets` go; `weakHeight`, `normalHeight`, `strongHeight`, `pushSeconds`, `budgetSlots`, `topSlotOpacity` and `lineStepPixels` come; `topLimitPixels` now limits a number's top.
- A number keeps its record's tick and entry number, its life and height, the cells where its enemy was last, its slot, its whole lift, the tick it began to leave and its order.
- `Show`, after its two skips of today: every number alive with the record's entry number takes the record's position, one slot more and the new number's height more of lift, and begins to leave on the record's tick if that takes it beyond the budget. The new number takes a label as today, at the record's position, slot 0, no lift.
- `LateUpdate`, for each number alive: gone at the end of its life or p after it began to leave; if an enemy on the board has its entry number, its position becomes that enemy's as drawn (the interpolation the word uses); its eased lift and slot are the whole ones less the share not yet done of each newer number of its enemy still younger than p, listed once per frame in an array made at start; then the top line, `translate`, `scale` and `opacity`.

**Done when:** the view has no collection keyed by an enemy; no per-frame code allocates or reads a measured size; every write to a label happens only on a change; Core has no `UnityEngine`, and the whole EditMode suite of 4c passes untouched.

### T4d-8 – Looks and scene
Serves 4d-AC6 to AC8 and AC16. Editor open on the worktree; after the stage 4 review. `Hud.uss` by hand, the scene through `unity command`.
- `Hud.uss`, section 2: the sizes of `damage-number--weak`, `--normal` and `--strong`.
- `Prototype.unity`, on `Hud`: the direction's values in the fields of T4d-6, the new height above the enemy and the new top limit. Save the scene, which drops the removed fields.

**Done when:** clean compile with an empty console; in Play two towers of the other element on one enemy show a column of gold numbers that walks with it, the newest lowest, none over another; the numbers of a killed enemy stay where it died and fade; on the top row they line up under the top bar; everything is saved and `.vscode/settings.json` is reverted.

### T4d-5 – WebGL build
Serves 4d-AC14. Editor closed; after the tests pass. As T3b-5 of plan 0005. The 4c build is kept, or rebuilt from `022fd75`, for the comparison below.

## Tests for the qa-tester
On the worktree with its editor closed. A test not listed under "Tests that change" that fails is reported before it is changed. Scene tests run with the time scale at 0 and tick by hand, so battle time is exact: 60 ticks are a second, and p is not a whole number of ticks, so expected values come from the formulas. Where an enemy is drawn is read from its enemy view. A record given to `Show` is stamped `Battle.TickCount`; one that names an entry number no enemy on the board has stands at its own position.

**Tests that change** (the refusal leaves the message line)
- `SceneTestBase`: helpers for the refusal label (shown, its text). `_label` and `MessageShown` stay the message line's.
- `PrototypeSceneTests`: the two 2b-AC4 tests and the 3c-AC5 test read the refusal label; the timing test follows the direction's life in place of 2 s. `GoldSceneTests`: the two 3b-AC3 tests read the refusal label.
- `BonusSceneTests`: the two 4a-AC4 tests where a refusal and the survived line replace each other become one where both show together and each keeps its own time; the defeat test's refusal case and its restart check read the refusal label.
- `ElementSceneTests`: the 4b-AC18 refusal test goes (4d-AC10 replaces it) and the survived test loses its refusal half; the "no refusal" checks of 4b-AC8 and the `MessageShown` checks after a click in `SelectionSceneTests` and `RunSceneTests` also check the refusal label; the 4b-AC14 allocation test adds both new views.

**EditMode**, on `Battle` with stats and a chart chosen per test
- **4d-AC1:** 0 at the start, n after n ticks; unchanged by `Tick()` after a win and after a loss.
- **4d-AC2:** `KindOf` for 0, 0.25, 0.75, 1, 2 and 1000. Source test: no file under `Presentation/` or `Gameplay/` contains `Multiplier`.
- **4d-AC3:** at x2 a shot of 10 adds a record with amount 20, strong, the target's position, the tick's count and the target's `Entry`; no element gives normal at 10; a chart row of 0.25 gives weak at 2.5. A hit of 20 on 5 health records 20 with the killed enemy's number, and the record is there after the enemy is gone. A shot whose target left adds none. Two hits on one tick are two records in landing order. Three enemies enter as 1, 2 and 3; after the first has left, the next enters on its object as 4, and its hits carry 4. With more than 64 hits: `HitCount` is exact, `HitAt` answers the last 64 and throws for the one before them, for −1 and for `HitCount`.
- **4d-AC4:** an immune landing leaves `HitCount` unchanged and sets the mark to that tick; a second landing moves it; two towers on one enemy leave one mark, the later tick; an enemy hit by the other element keeps 0; a reused object starts at 0.
- **4d-AC5:** the suite of 4c untouched; the determinism test also compares `HitCount` and the last record after every tick; `TickAllocation.AssertNoneAcross` over a run with strong and immune landings past 64 hits.

**PlayMode**, in `Prototype.unity`
- **4d-AC6, real hits:** a tower of the other element beside the route. On the frame after the first landing: exactly one number, its text the record's amount (20 with today's assets), the strong class resolved to the direction's size, colour and outline, at scale 0. Then tick by tick up to p: the scale of the formula, 1 from p on, and on every tick its bottom centre the direction's height above the enemy view.
- **4d-AC6, several ticks in a frame:** tick 30 times with no frame while two towers land hits: the next frame shows one number per record still within its life; the same ticks run one per frame end with the same numbers at the same translate, scale and opacity. Then the reference order run for 3000 ticks with no frame, and one frame: nothing throws and only records within their life show.
- **4d-AC6 to AC8, through `Show`**, the records naming the first enemy on the board unless stated:
  - one of each kind has its class, its resolved look and its life; opacity 1 up to the direction's share, half-way down between it and the end, hidden on the tick its life ends;
  - amounts 2.4, 2.5, 0.2 and 16 read `2`, `3`, `1` and `16`, also with a decimal-comma culture;
  - a second record 0, 1, 2, 3, 5 and 10 ticks after the first: on every tick the older number's lift is the formula's and the two rectangles do not intersect; a third adds its lift; a weak one under a strong one lifts it by the weak height, and the strong one stays up after the weak one ends;
  - four records six ticks apart: slots 0 to 2 fill, the number in the last slot is at the direction's opacity, and the fourth makes the first leave: lifted one height more, fading to 0 over p from that tick, then hidden with its label free. Five records on one tick leave three and two leaving;
  - a record naming another enemy lifts none of the first enemy's numbers;
  - 45 records, each naming an entry number of its own that is not on the board, at the cap of 40 leave 40 alive, reading 6 to 45;
  - with 29 alive a weak one shows; with 30 it shows none and lifts none, a normal one does, and the weak one shows again once fewer than 30 are alive;
  - the number of elements under `damage-numbers` never changes.
- **4d-AC9, deterministic by ticks:** a tower of wave 1's own element beside the route.
  1. No word before the first landing. On the frame after it: one word on that enemy with the direction's text and look, hung by its top centre at the direction's place below the enemy view, no number, and `HitCount` 0.
  2. Through the next three landings, on every frame: still exactly one word, fully opaque (the tower lands every 40 ticks, inside the hold), and it follows the enemy view.
  3. A second tower of that element on the same enemy: still one word.
  4. Sell both and let their shots in flight land: the word is opaque 45 ticks after the enemy's mark, at half opacity 54 ticks after it and hidden from 60.
  5. Two enemies in range, as one leaves it: each carries its own word and the first fades on its own time. An enemy that exits while its word shows: on the next frame no word stands where it was, and the words left belong to enemies that have a mark.
  6. A tower of the other element added on a marked enemy: numbers and the word show together.
- **4d-AC10:** each of the five reasons shows its text in `refusal`, hung at the direction's height above the clicked cell, in the direction's look; nothing on the message line. In real seconds: the direction's rise by its end, opaque until its fade starts, hidden after its life. A second refusal on another cell moves it there with the new text and a full life. On a corner cell and on a top-row cell its rectangle lies the direction's margin inside `board-area`, at the game view's aspect and at 1 and 2.5. With the survived line showing, a refusal leaves that line's text, class and time alone.
- **4d-AC11:** with a number, a word and a refusal on screen, tick to a defeat and, in another test, to a win: on the next frame none shows, and none after a second of real time. After a restart each shows again.
- **4d-AC12:** `Hover` and `Click` at the screen point of a shown number, word and refusal give the hover cell and the purchase of that cell; every element under `floating-texts` ignores picking.
- **4d-AC13:** with 40 numbers alive through `Show` and a word on every enemy of a wave, `Allocations.Of` the `LateUpdate` of `DamageNumberView` and of `RefusalView` over 120 ticks with real landings of amounts seen before: 0. On frames with no new hit and no refusal: no label's `layout` changes, no label under `floating-texts` gets a `GeometryChangedEvent`, and the texts are the same string objects.
- **4d-AC14:** the scene tests of 2b to 4c pass with the changes above, the reference order's win on its tick with its gold included.
- **4d-AC15:**
  - through `Show` on a walking enemy: on every tick through a turn and a diagonal step the numbers' bottom centres are the direction's height and their lifts above the enemy view;
  - real hits that kill an enemy with numbers showing: from the next frame all of them, the killing hit's at slot 0, stand over the position of the killing record with their lifts, do not move, also after the camera's aspect changes, and end on their own ticks;
  - an enemy that exits with a number showing: the number stays where the enemy view last stood;
  - after an enemy with a number showing has left and the next has entered on its object: the number has not moved, and a record naming the new enemy enters at slot 0 over the new enemy and lifts nothing of the old one.
- **4d-AC16, through `Show`** with records that name no enemy on the board: three at a top-row cell stand with their tops on the limit at 0, 1 and 2 steps to the right, and on no tick of the pushes is a top above the limit; three at a second-row cell stand in a column with only slot 2 on the line, 2 steps to the right; the fourth record makes the first leave along the line.
- **4d-AC18 (a):** on the empty board, `Show` with strong records naming the first enemy of wave 1 over its whole walk, on the ticks six towers would land (every 40 ticks, the first tick of each 0, 0, 1, 3, 7 and 12 ticks into the cycle): on every tick no two rectangles (`worldBound` of the visible labels under `damage-numbers`) overlap, unless one of the two stands on the top line (its bottom is its height below the limit). The walk passes the second row, where the last slot reaches the line, so the test also asserts that some frames had a number there.
- **4d-AC18 (b), through `Show`** with strong records that name no enemy on the board, at a top-row cell, on the ticks five towers would land spread evenly (every 40 ticks, 8 ticks apart), for 480 ticks: the frames with an overlap, over the frames with at least one number, are at most the direction's share.
- **4d-AC18 (c):** the reference order to the tick its last wave starts, then one frame per tick to the win: the frames with an overlap between any two numbers, over the frames with at least one number, are at most the direction's share for numbers of different enemies. Both tests log their share. They replace the developer's frame log and script, which stay outside the repository.

**Orchestrator in the browser, then Eduardo**
- **The measurement.** In Chrome's Performance panel, record ten seconds of wave 9 or 10 of a full build (about twenty enemies, a dozen or more towers firing), on the 4c build and on this one, each also with the CPU slowed four times. This build passes when it drops no frame the 4c build holds and its scripting time over the ten seconds is at most 600 ms above 4c's (1 ms a frame), and the memory line shows no new sawtooth. If it fails: lower the cap first, then the fallback of ADR 0006.
- Two or three towers of the other element on one enemy: is the newest number clear, do the older ones step up without covering one another, and is it plain whose they are as the enemy walks?
- A kill: the last numbers stay where the enemy died and fade; the next enemy walks past them.
- A tower of the wave's element: does the grey "immune" explain the wasted shots without a second look? Does it stay steady, with no flicker between shots, and leave soon after the enemy walks out of range?
- Late waves: with the board full and several towers on the lead enemy, do the numbers stay readable or become noise? Do they hide enemies or health bars?
- An enemy in the top row: its numbers line up under the top bar and are never cut off. An enemy hit by both elements: the numbers above it and the word below it, apart, with the health bar between them.
- A refused click on a wall, the spawn, with no gold, on an enemy and on a cell that would close the path: the reason shows at the cell, also at the board's edges, and the bottom bar stays quiet. A survived line and a refusal together.
- The text is sharp while it grows and moves, in Chrome and Safari, on the canvas and in full screen. Under the end screen nothing floats. The console shows no error.

## Risks
- **Numbers still meet, within the targets.** By design: a stack reaches the enemy ahead on a vertical stretch and the lane one cell above, the numbers of a dead enemy stand in the way of the next, and on the top line a new number touches the one beside it as it enters. The simulation gives 6.6 % for 4d-AC18 (c), 5.9 % of it between different enemies, and 12 % for (b). A measured share over its target returns to the art-director, not to values in the scene.
- **Churn under focused fire.** In the simulation a fifth of the last wave's numbers leave before their life ends and the shortest shows 0.21 s. More towers on one enemy shorten that further: one number per hit has no answer to it but the budget and the lives.
- **Jumps at the top line.** A number steps sideways in one frame when its enemy walks its place across the line, and on the top row's last column the line runs over the side column.
- **WebGL cost.** Reasoned, not measured, until the browser check above (ADR 0006). Per frame and number alive: a search of the enemies on the board for one whole number, a pass over the numbers still entering, one `translate`, a `scale` during its first p and an `opacity` while it fades; a word per enemy as before. All of it is arithmetic on at most 40 numbers. Per hit: a cached string and a class, written only when the label held another.
- **GC.** A string is built the first time an amount occurs, and a word's anchor when more enemies are on the board than ever before; nothing else allocates after start.
- **A changed text dirties layout up to the root.** The labels are absolute and `board-area` keeps its rectangle, so the camera framer does not reframe; the 4d-AC13 test would show it if it did.
- **The entry number replaces the count a shot compares.** The 2c tests of a shot at an enemy that left guard it; they pass untouched or the change is wrong.
- **Text scaled and between pixels.** A growing or walking label is not snapped to whole pixels, and its outline scales with it. If it shimmers, the anchor is rounded to the physical pixel: one line in the view.
- **Two clocks.** Numbers and words run in battle time and the refusal in unscaled time, on purpose. At a later game speed of 2 a number lives half as long on the wall clock, with twice the hits.
- **The last hit is never seen.** The kill that wins the run shows its number for no frame: everything hides with the end screen.
- **Tests reach weak and normal only through `Show` and a chart of their own.** No scene content produces them, so their first real appearance is checked when the content arrives.
- **Log capacity against the cap.** 64 records against a cap of 40: a raise of the cap above 64 in the scene would lose numbers on a long frame, not damage.
- **Coupling.** `DamageNumberView` reads the bootstrap, the camera, `Battle` and its enemies, as `CombatView` does; it knows no other view.
