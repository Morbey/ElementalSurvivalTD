# Technical plan 0009 – Prototype slice 4, step 4d

Damage numbers over the enemies, the word "immune" over an enemy that towers waste shots on, and the placement refusal at the refused cell. Architecture: `docs/adr/0006-hit-log-text-over-the-board.md`, with `docs/adr/0005-elements-chart-damage-rule.md` for the kind of a hit. Looks: `docs/art/visual-direction.md` ("the visual direction"), sections 2 and 1.3. Numbers do not change: `docs/balance/0001-prototype-economy.md` stays as it is. Prototype mode: this plan carries the acceptance criteria. It builds on plan 0008 and reuses "Editor sequencing" of plan 0007.

**What Eduardo sees:** every hit pops a gold number over the enemy; an enemy that a tower cannot hurt carries a grey "immune" for as long as the shots keep coming, so a tower shooting its own element no longer looks broken; and a refused click says why at the cell he clicked.

**Not cut.** The refusal is about a quarter of the work, shares the numbers' mechanism and has its own task, so a second cycle of tests, build and acceptance would cost more than it shows. If the refusal stalls in review or testing, T4d-3, the `refusal-anchor` element and their tests leave the step with no change to the rest: the numbers ship and the refusal stays on the message line.

## Approach
**Core records, views read.** `Battle` counts its ticks. Where a shot lands it already reads one multiplier; the chart now names the kind of hit from that number, and the hit becomes a record in a fixed log of the last 64: position, damage dealt, kind and tick. A view remembers how many hits it has read and reads the ones after, so a frame that ran five ticks sees all five ticks' hits and a tick calls nothing outside Core. A hit on an immune enemy adds no record: the enemy remembers the tick of the last one, and the view asks each enemy on the board. That keeps "once per enemy, alive while the shots continue" out of the view: it holds no map of enemies, and the n-th word shows the n-th enemy as the n-th enemy view does (ADR 0006).

**Text over the board is UI Toolkit labels moved by transform.** One element of the HUD holds a fixed pool of labels. A label is moved by `translate` and faded by `opacity`, never by layout, which is what ADR 0003 rejected for health bars; sizes, colours and outlines are USS classes, in the panel pixels the visual direction states them in; a text or a class is written only when the pooled label held another. A number ages in battle time (tick count plus the fraction of a tick), so it stands still while the battle does and a test ticks it by hand. The refusal is the same kind of label in unscaled time, as the message line is: it is feedback on a click and must last as long at any game speed. The cost of this choice against TextMeshPro is reasoned in ADR 0006 and measured in the browser by this step.

**What exists decides what is built.** Core answers four kinds because the multiplier has four cases (ADR 0005, decision 5). Today's assets only produce strong and immune; normal needs "no element" and weak a chart row below 1, so tests reach both through a chart of their own and through the view's `Show`. Weak gets its look and its place in the cap now: Core must answer something below 1, and the first x0.25 row would otherwise show wrong with no code change. Nothing of a critical hit is built: no field, no look, no pop.

Discarded: Core events for a hit, a buffer cleared each tick, the immune word kept by the view per enemy, TextMeshPro meshes, a hand-drawn digit mesh, one element drawing all numbers, USS transitions for the motion and a world-space panel (ADR 0006); an enemy reference in the record (a killing hit must still show); immune hits in the log (the mark on the enemy says it once); the amount rounded in Core (how it is written is a look); one view for numbers and refusal (one polls the battle, the other answers an input event; they share only where a cell lies on the panel); a general pool that grows (the cap is the pool).

## Acceptance criteria
- **4d-AC1 Tick count.** `Battle.TickCount` is 0 before the first tick and rises by one for every `Tick()` that runs; a finished battle counts no more.
- **4d-AC2 Kind of a hit.** The kind is decided in Core from the one multiplier read on landing: immune at 0, weak below 1, normal at 1, strong above 1. No file under `Presentation/` or `Gameplay/` reads a multiplier.
- **4d-AC3 Hit log.** Every shot that lands on a target that is not immune adds one record, the killing hit included: the target's position in cells on that tick, the amount (the shot's damage times the multiplier: what `TakeDamage` receives, not cut to the health left), the kind and the tick. A shot dropped because its target left adds none. `HitCount` counts the records since the run began; the log answers for the last `HitLogCapacity` (64) of them, in landing order, and throws for any other index. Hits beyond that are overwritten oldest first; their damage is dealt all the same.
- **4d-AC4 Immunity mark.** A shot that lands on an immune target adds no record; the enemy's `ImmuneHitTick` becomes that tick. It is 0 from every entry until the first such shot, also on a reused object.
- **4d-AC5 Rules untouched.** Damage, targeting, gold, waves and the tick order do not change: every EditMode test of 4c passes untouched, 3b-AC10 holds, and `Tick()` allocates nothing, also past 64 hits.
- **4d-AC6 A number per hit.** From the first frame drawn after the tick of a record, one number shows for it, hung by its bottom centre at the board point of the record raised by the direction's height, plus the next offset of the direction's cycle (one counter for all numbers). It stays at that board point, also through a resize, and does not follow the enemy. Its text is the amount rounded to the nearest whole number, a half up, and at least 1, written once per amount with the invariant culture; its look is its kind's. A frame that ran several ticks shows every hit of those ticks, each aged from its own tick.
- **4d-AC7 Motion and life.** A number's age is `(TickCount − its tick + the fraction of a tick left over) × TickSeconds`. With t its age over its kind's life: it has risen the direction's distance times `1 − (1 − t)²`, is fully opaque up to the direction's share of its life and then fades linearly to 0, and is gone at t = 1.
- **4d-AC8 Caps.** At most the direction's cap of numbers is alive. A hit takes a free label; with none free it takes the oldest number's. From the direction's cut-off alive, a weak hit shows no number. The labels are made at start: none is created or destroyed afterwards.
- **4d-AC9 Immune word.** Each enemy on the board whose `ImmuneHitTick` is not 0 and younger than the direction's hold, in battle time, carries one word, however many towers hit it: the direction's text and look, at the direction's height above the enemy as it is drawn, following it; fully opaque, then fading linearly over the direction's last part of the hold. An immune hit shows no number, and words do not count towards the cap. The word is gone on the first frame after its enemy has left the board.
- **4d-AC10 Refusal at the cell.** `PlacementRefused` carries the reason and the cell; every refusal has one. The reason's text, unchanged, shows in one label whose bottom centre starts the direction's height above the cell's centre, rises and fades as section 1.3 states, in unscaled seconds, and is kept the direction's margin inside the board area on every frame. A newer refusal restarts it at its own cell. The message line shows only the survived line: a refusal neither shows there nor replaces it, and both may be on screen together. This replaces where and how long a refusal shows in 2b-AC4, 4a-AC4 and 4b-AC18; the text per reason holds, and so does the rest of 4a-AC4 and 4b-AC18 for the survived line.
- **4d-AC11 End of a run.** From the first frame after the run is won or lost, no number, word or refusal shows. After a restart all three work again.
- **4d-AC12 Clicks.** No floating text takes picking: a hover or a click on a cell under one does what it does without it (4b-AC8).
- **4d-AC13 Layers and cost.** Core still compiles with `noEngineReferences: true`; the views hold no rule, number, colour or text of their own. With the cap of numbers alive and a word on every enemy, both views allocate 0 bytes per frame after warm-up, and a tick allocates nothing (4d-AC5). A text is built only the first time an amount occurs. On a frame with no new hit and no refusal, no text and no class is written and no label's layout rectangle changes.
- **4d-AC14 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari, and passes the measurement under "Orchestrator in the browser". Everything of 2b to 4c that this plan does not replace still holds.

The sizes, colours, outlines, lives, distances, offsets, caps and wording are those of the visual direction, section 2 and section 1.3 ("Placement refusal at the cell").

## What this step takes from the visual direction
| Section of the direction | Where the values live |
|---|---|
| 2: size, weight, colour and outline of weak, normal, strong and the immune word | `Hud.uss`, one class each |
| 2: the life per kind, the height above the enemy, the rise, the share of life at full opacity, the offset cycle, the cap, the weak cut-off, the immune hold and its fade, the word | Fields of `DamageNumberView` in the scene |
| 1.3 refusal: size, weight, colour, outline | `Hud.uss` |
| 1.3 refusal: the height above the cell, the rise, the life, the fade, the margin inside the board area; the five texts, moved unchanged from the message view | Fields of `RefusalView` in the scene |
| 1.3 bottom bar: the message line without its refusal kind | `Hud.uss`: `message--refusal` goes |

Not in this plan: the critical row of section 2 and its pop; a critical-hit rule; x0.25 content; gold pop-ups, a death effect and sound; health bars hidden until the first damage (Parking lot).

**Read of the direction, for the art-director to confirm.** Where section 2 is silent this plan takes: a half rounds up (2.5 shows 3); a number hangs by its bottom centre, as the refusal does; "ease-out" is `1 − (1 − t)²`; the offsets are panel pixels, so a negative y is up. Two things are built as written and need a ruling after the first build:
1. **The top row.** On the 960 x 540 canvas a number over an enemy in the top row starts about 5 pixels under the top bar and rises over its text; the direction clamps the refusal inside the board area and says nothing for numbers. Built as written, drawn above the bars so it stays whole. The cheap alternative is the refusal's clamp on the number's height.
2. **Word and number on one enemy.** The word and the first offset of the cycle share one point, so an enemy hit by a tower of each element shows a gold number on top of "immune". Built as written; a height of its own for the word would separate them.

## Tasks
Order and editor use: plan 0007, "Editor sequencing", with `tools/unity-lock.sh` around every batch run (`docs/unity-cli-notes.md`). T4d-1 to T4d-3 are code only and run in parallel on the signatures below; no balance task.

### T4d-1 – Core: the tick count, the kind, the log and the mark
Serves 4d-AC1 to AC5. Code only.

```csharp
public enum HitKind { Immune, Weak, Normal, Strong }

public readonly struct Hit
{
    public readonly float X, Y;          // the target's position in cells on the tick the shot landed
    public readonly float Amount;        // the damage dealt: the shot's damage times the multiplier
    public readonly HitKind Kind;        // never Immune in the log
    public readonly int Tick;            // Battle.TickCount on that tick
    public Hit(float x, float y, float amount, HitKind kind, int tick);
}

// ElementChart: public static HitKind KindOf(float multiplier);   // by its own two constants: 0 immune, 1 not listed
// Enemy:        public int ImmuneHitTick { get; private set; }     // 0: none since it entered; reset by Enter
// Battle:       public const int HitLogCapacity = 64;
//               public int TickCount { get; private set; }
//               public int HitCount { get; private set; }
//               public Hit HitAt(int index);                       // HitCount - HitLogCapacity (at least 0) to HitCount - 1
```

- `Tick()` raises `TickCount` first, after its check of the outcome, so everything stamped during a tick carries the value the count has once that tick has run.
- `StepProjectiles`, on landing: the multiplier as today, then `KindOf`. The product `Damage * multiplier` is computed once and goes to `TakeDamage` as today. An immune kind sets the target's mark; any other writes the record at `HitCount % HitLogCapacity`, before the target can be removed. The array is made in the constructor.

**Done when:** Core has no `UnityEngine`; the damage expression is the one of today; `Tick()` has no `new` on a tick with hits; the whole EditMode suite of 4c passes untouched.

### T4d-2 – Presentation: the numbers and the word
Serves 4d-AC6 to AC9 and AC11 to AC13. Code and `Hud.uxml`; in parallel with T4d-1.

```csharp
// Where a board position lies on the panel. Built once per frame from two camera reads; a label then costs arithmetic.
internal readonly struct BoardToPanel
{
    public BoardToPanel(Camera camera, IPanel panel, MapDefinition map);
    public Vector2 Point(float cellX, float cellY);          // panel coordinates of a position in cells
}

// DamageNumberView
public void Show(in Hit hit);                                // what the poll calls for each new record; public for tests
```

- `Hud.uxml`: between `bottom-bar` and `end-screen`, an element `floating-texts` that fills the screen and holds, in this order, `immune-words`, `damage-numbers` and `refusal-anchor` with the label `refusal`; all ignore picking. `Hud.uss` gets only their place: each text is a zero-size anchor of class `floating-anchor` with a label of class `floating-text` that the style sheet hangs by its bottom centre, so no code reads the size of a number. T4d-4 styles them.
- New `DamageNumberView.cs`: a serialized document, bootstrap, camera and the fields of the table above. At start it makes the cap's number of anchors under `damage-numbers` and `prewarmWords` of them under `immune-words` (the word's text set once), each marked `UsageHints.DynamicTransform` and hidden by visibility. In `LateUpdate`:
  - Once the run is not running it hides everything it shows and returns.
  - It calls `Show` for every record from the count it last read, or from `HitCount − HitLogCapacity` if that is later, up to `HitCount`.
  - `Show` skips a hit already past its kind's life and a weak hit from the cut-off; otherwise it takes a free anchor, or the oldest number's, and writes the text from a cache by rounded amount and the kind's class, each only when it differs from what that label holds.
  - For each number alive: its age; past its life it is hidden; otherwise `translate` from `BoardToPanel.Point`, the offset and the rise, and `opacity` when it differs from the one written.
  - For the n-th enemy on the board, the n-th word: shown while 4d-AC9 says so, at the enemy's interpolated position (as `CombatView` draws it); the pool grows only when more enemies are on the board than words exist.

**Done when:** the view holds no rule, number, colour or text of its own; no per-frame code allocates; visibility, opacity, text and class are written only on a change; no code reads a measured size.

### T4d-3 – Gameplay and Presentation: the refusal at the cell
Serves 4d-AC10 to AC13. Code only; in parallel, on the UXML names and `BoardToPanel` of T4d-2.

- `TowerPlacementInput.cs`: `public event Action<PlacementResult, GridPos> PlacementRefused;`, raised with the clicked cell. No refusal is left without one: a click off the grid raises nothing.
- New `RefusalView.cs`: a serialized document, placement input, bootstrap, camera, the five texts and the fields of the table above. On a refusal it writes the text, measures it once, and keeps the cell and `Time.unscaledTime`. In `LateUpdate`, while shown: hidden once its life is over or the run is not running; otherwise the anchor is at `BoardToPanel.Point` of the cell raised by the height, less the rise so far, clamped by the margin and the measured size inside the rectangle of `board-area`, with the opacity of 4d-AC10.
- `MessageView.cs`: the placement input, the five texts, `refusalDuration`, the refusal class, the subscription and the `refusal` parameter go; it shows the survived line only.
- Tests, to compile only: the two `PlacementRefused` lambdas in `SelectionSceneTests` take two arguments.

**Done when:** no code file mentions `message--refusal` or `refusalDuration`; the view holds no rule, number, colour or text of its own; `LateUpdate` allocates nothing.

### T4d-4 – Looks and scene
Serves 4d-AC6 to AC10. Editor open on the worktree; after T4d-1 to T4d-3 and the stage 4 review. Through `unity command`, except `Hud.uss`:
- `Hud.uss`, sections 2 and 1.3: the classes `damage-number--weak`, `damage-number--normal`, `damage-number--strong` and `immune-word`, and `#refusal`, each with its size, weight, colour and outline. A floating label takes nothing from `.hud-text`. The rule `#message.message--refusal` goes.
- `Prototype.unity`, on `Hud`: a `DamageNumberView` and a `RefusalView` with their references and the direction's values; the five refusal texts as they are on the message view today; `prewarmWords` equal to the combat view's `prewarmEnemies`. Save the scene, which drops the message view's removed fields.

**Done when:** clean compile with an empty console; in Play a tower of the other element pops gold numbers that rise and fade over its target, a tower of the wave's element puts one "immune" on its target that leaves a second after the last shot, a click on a wall shows its refusal at that cell and nothing on the message line; everything is saved and `.vscode/settings.json` is reverted.

### T4d-5 – WebGL build
Serves 4d-AC14. As T3b-5 of plan 0005. The 4c build is kept, or rebuilt from `022fd75`, for the comparison below.

## Tests for the qa-tester
On the worktree with its editor closed. A test not listed under "Tests that change" that fails is reported before it is changed. Scene tests run with the time scale at 0 and tick by hand, so battle time is exact: 60 ticks are a second.

**Tests that change** (the refusal leaves the message line)
- `SceneTestBase`: helpers for the refusal label (shown, its text). `_label` and `MessageShown` stay the message line's.
- `PrototypeSceneTests`: the two 2b-AC4 tests and the 3c-AC5 test read the refusal label; the timing test follows the direction's life in place of 2 s. `GoldSceneTests`: the two 3b-AC3 tests read the refusal label.
- `BonusSceneTests`: the two 4a-AC4 tests where a refusal and the survived line replace each other become one where both show together and each keeps its own time; the defeat test's refusal case and its restart check read the refusal label.
- `ElementSceneTests`: the 4b-AC18 refusal test goes (4d-AC10 replaces it) and the survived test loses its refusal half; the "no refusal" checks of 4b-AC8 and the `MessageShown` checks after a click in `SelectionSceneTests` and `RunSceneTests` also check the refusal label; the 4b-AC14 allocation test adds both new views.

**EditMode**, on `Battle` with stats and a chart chosen per test
- **4d-AC1:** 0 at the start, n after n ticks; unchanged by `Tick()` after a win and after a loss.
- **4d-AC2:** `KindOf` for 0, 0.25, 0.75, 1, 2 and 1000. Source test: no file under `Presentation/` or `Gameplay/` contains `Multiplier`.
- **4d-AC3:** at x2 a shot of 10 adds a record with amount 20, strong, the target's position and the tick's count; no element gives normal at 10; a chart row of 0.25 gives weak at 2.5. A hit of 20 on 5 health records 20, and the record is there after the enemy is gone. A shot whose target left adds none. Two hits on one tick are two records in landing order. With more than 64 hits: `HitCount` is exact, `HitAt` answers the last 64 and throws for the one before them, for −1 and for `HitCount`.
- **4d-AC4:** an immune landing leaves `HitCount` unchanged and sets the mark to that tick; a second landing moves it; two towers on one enemy leave one mark, the later tick; an enemy hit by the other element keeps 0; a reused object starts at 0.
- **4d-AC5:** the suite of 4c untouched; the determinism test also compares `HitCount` and the last record after every tick; `TickAllocation.AssertNoneAcross` over a run with strong and immune landings past 64 hits.

**PlayMode**, in `Prototype.unity`
- **4d-AC6, AC7, real hits:** a tower of the other element beside the route. On the frame after the first landing: exactly one number, its text the record's amount (20 with today's assets), the strong class, its bottom centre at the record's board point raised by the direction's height (through `RuntimePanelUtils.CameraTransformWorldToPanel`), resolved to the direction's size, colour and outline. Then tick by tick: the rise at a quarter, a half and the whole of its life, opacity 1 up to the direction's share and half-way down between it and the end, hidden on the tick its life ends. After the camera's aspect changes it is still at its board point.
- **4d-AC6, several ticks in a frame:** tick 30 times with no frame while two towers land hits: the next frame shows one number per record still within its life, each at the rise of its own age; the same ticks run one per frame end with the same numbers at the same places. Then the reference order run for 3000 ticks with no frame, and one frame: nothing throws and only records within their life show.
- **4d-AC6 to AC8, through `Show`** with records stamped `Battle.TickCount`:
  - one of each kind has its class, its resolved look and its life (the direction's seconds times 60, in ticks);
  - amounts 2.4, 2.5, 0.2 and 16 read `2`, `3`, `1` and `16`, also with a decimal-comma culture;
  - six at one point stand at the cycle's five offsets and the sixth on the first;
  - amounts 1 to 45 at the cap of 40 leave 40 alive, reading 6 to 45;
  - with 29 alive a weak one shows; with 30 it does not, a normal one does, and the weak one shows again once fewer than 30 are alive;
  - the number of elements under `damage-numbers` never changes.
- **4d-AC9, deterministic by ticks:** a tower of wave 1's own element beside the route.
  1. No word before the first landing. On the frame after it: one word on that enemy with the direction's text and look, at the direction's height above the enemy view, no number, and `HitCount` 0.
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

**Orchestrator in the browser, then Eduardo**
- **The measurement.** In Chrome's Performance panel, record ten seconds of wave 9 or 10 of a full build (about twenty enemies, a dozen or more towers firing), on the 4c build and on this one, each also with the CPU slowed four times. This build passes when it drops no frame the 4c build holds and its scripting time over the ten seconds is at most 600 ms above 4c's (1 ms a frame), and the memory line shows no new sawtooth. If it fails: lower the cap first, then the fallback of ADR 0006.
- A tower of the other element: is the gold number read at a glance, and is it clear which enemy it belongs to once the enemy has walked on?
- A tower of the wave's element: does the grey "immune" explain the wasted shots without a second look? Does it stay steady, with no flicker between shots, and leave soon after the enemy walks out of range?
- Late waves: with the board full, do the numbers stay readable or become noise? Do they hide enemies or health bars?
- An enemy in the top row: the number rises over the top bar's text (ruling 1). An enemy hit by both elements: the number over the word (ruling 2).
- A refused click on a wall, the spawn, with no gold, on an enemy and on a cell that would close the path: the reason shows at the cell, also at the board's edges, and the bottom bar stays quiet. A survived line and a refusal together.
- The text is sharp while it rises, in Chrome and Safari, on the canvas and in full screen. Under the end screen nothing floats. The console shows no error.

## Risks
- **WebGL cost.** Reasoned, not measured, until the browser check above (ADR 0006). Per frame: two camera reads per view, one `translate` per label alive (at most 40 numbers and a word per enemy) and one `opacity` per label that fades. Per hit: a cached string and a class, written only when the label held another; with today's assets every number is the strong `20`, so nearly none. A text or class that does change lays out one absolute label, about 18 times a second with a dozen towers.
- **GC.** A string is built the first time an amount occurs, and a word's anchor when more enemies are on the board than ever before; nothing else allocates after start.
- **A changed text dirties layout up to the root.** The labels are absolute and `board-area` keeps its rectangle, so the camera framer does not reframe; the 4d-AC13 test would show it if it did.
- **Outline at small sizes.** A one-pixel outline on 16-pixel text at the canvas scale of 0.95 may read heavy or thin; the art-director judges it in the build.
- **Positions between pixels.** A rising label is not snapped to whole pixels. If the text shimmers, the anchor is rounded to the physical pixel: one line in each view.
- **Two clocks.** Numbers and words run in battle time and the refusal in unscaled time, on purpose. At a later game speed of 2 a number lives half as long on the wall clock, with twice the hits.
- **The last hit is never seen.** The kill that wins the run shows its number for no frame: everything hides with the end screen.
- **Tests reach weak and normal only through `Show` and a chart of their own.** No scene content produces them, so their first real appearance is checked when the content arrives.
- **Log capacity against the cap.** 64 records against a cap of 40: a raise of the cap above 64 in the scene would lose numbers on a long frame, not damage.
- **A renamed event signature.** Every subscriber compiles or fails: the message view's goes, the refusal view's and two test lambdas change.
- **Coupling.** Both new views read the bootstrap and the camera; `DamageNumberView` reads `Battle` and its enemies, as `CombatView` does; neither knows the other, nor any other view.

## Follow-up after acceptance
- The visual direction, section 1.3: the two clauses that say the refusal uses the message line "until" this step are stale (the art-director's file).
