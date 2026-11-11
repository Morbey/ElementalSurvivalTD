# Technical plan 0007 – Prototype slice 4, steps 4a and 4b

A gold bonus for each wave survived (4a), then Light and Shadow towers and waves (4b). Architecture: `docs/adr/0004-run-waves-outcome-restart.md`, amended for wave groups, the bonus and clicks on the UI, and `docs/adr/0005-elements-chart-damage-rule.md`. Numbers: `docs/balance/0001-prototype-economy.md` ("the balance document"), re-proposed for each step. Looks: `docs/art/visual-direction.md` ("the visual direction"). Prototype mode: this plan carries the acceptance criteria.

## Steps
| Step | What Eduardo sees |
|---|---|
| **4a** Wave bonus | When the last enemy of a wave has left, a line such as "Wave 3 survived +25" shows and the gold rises, also when enemies leaked. |
| **4b** Light and Shadow | Two buttons with a price, towers, enemies and shots in the colour of their element, each wave announced with its element, x2 against the other element, and shots wasted on an immune enemy. |

4a goes first: it is small and it changes the wave format that 4b builds on. 4b is not cut again: leaving the buttons for a third step would save about a tenth of its work and cost one more cycle of tests, build and acceptance. Damage numbers and then mixed waves follow, each with its own plan.

## Approach
**4a.** A wave becomes the unit the design talks about: its delay, its survival bonus and its groups of enemies in spawn order. Every wave has one group for now and `WaveSchedule` refuses any other number, so the asset format, the wave count, the countdown and the bonus already hold for a mixed wave: it will be a wave with several groups, and its step adds the walk over them in `WaveSchedule`, the gap between two groups and the content. Each enemy remembers the wave it entered with. After the projectiles and before the outcome, `Battle` pays every wave whose last enemy has entered, that has no enemy on the board and that has not paid, while a life is left: one pass over a few dozen enemies, with no counter to keep in step. The last wave pays like the others, on the tick that wins. The message line polls a count of survived waves, so the tick still calls nothing outside Core.

**4b.** An element is an asset with an id; towers and enemies carry it in their stats, and one chart asset holds the multipliers (ADR 0005). Targeting does not change. The element enters in one place, where a shot lands: `Battle` asks the chart for one multiplier, 0 against the own element, and the target loses the damage times it. The step after this one records that same number as the kind of hit. The modifier system stays deferred (ADR 0005, decision 9).

The placement input holds the list of buyable towers and which one is selected; number keys and the buttons of a new bar select. Before it picks a cell it asks the UI panel what is under the pointer, which is the general click rule ADR 0004 deferred. Colours come from one palette asset in Presentation; a tower gets its material when placed, and a pooled enemy or shot view changes material only when it shows another element.

This plan builds mechanisms and chooses no colour, size, position or wording: those are values of the visual direction, held in materials, the palette, `Hud.uss`, `Hud.uxml` and the text fields of the scene, and applied by the assets task of each step once that document is validated. What it can ask for without new code is listed under "What the visual direction fills in".

Discarded: a counter of enemies per wave and a Core event for a survived wave (ADR 0004); an enum of elements, immunity as chart data, the multiplier at launch and materials on the element asset (ADR 0005); walking several groups now (mixed waves are the step after damage numbers); a selection kept in Core (it is input state); fixed buttons in UXML (the bar is built from the tower list); an `EventSystem`.

## Acceptance criteria – 4a
- **4a-AC1 Wave shape.** A wave has a delay, a survival bonus and one or more groups in spawn order; a group has an enemy definition, a count and a spawn interval. The wave count, the countdown and the bonus count waves, never groups. Nothing in Core keeps an enemy or an element on the wave itself. In this slice every wave has exactly one group and `WaveSchedule` refuses any other number; 3a-AC1 is otherwise unchanged.
- **4a-AC2 Survived.** A wave is survived on the first tick, judged after the projectiles and before the outcome, on which its last enemy has entered, none of its enemies is on the board, however they left, and at least one life is left. Waves are judged one by one, so they may be survived out of order or on the same tick, each once. An enemy that entered outside a wave counts for none.
- **4a-AC3 Bonus.** On that tick the gold rises by the wave's survival bonus, a whole number of at least 0. A wave whose enemies all reached the exit still pays. Nothing is paid on the tick that takes the last life or after the run has ended. The last wave pays on the tick that wins.
- **4a-AC4 On screen.** From the frame after, the gold label shows the new gold (3b-AC7) and the message line shows the survived text with the wave's number and its bonus, for as long as a refusal message stays. A newer message of either kind replaces the one shown. The wording, the place and the style are those of the visual direction.
- **4a-AC5 Data.** The bonus of each wave comes only from `Run_Prototype.asset`. Every value of 3b-AC8 and the bonuses equal the balance document. This replaces 3c-AC7.
- **4a-AC6 Design target.** 3b-AC9 holds with the runs of the balance document for this step, bonuses included. This replaces 3c-AC8.
- **4a-AC7 Core and determinism.** The bonus is a rule of `ElementalTD.Core`, in whole numbers. `Tick()` runs waves, enemies, towers, projectiles, wave bonuses and the outcome, in that order. 3b-AC10 holds.
- **4a-AC8 Allocation.** 3b-AC11 holds on ticks that pay a bonus. The message text is built only when a wave is survived.
- **4a-AC9 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari. Everything of 2b to 3c that this step does not replace still holds.

## Acceptance criteria – 4b
- **4b-AC1 Elements are data.** Light and Shadow are `ElementDefinition` assets with an id and a display name. The multiplier of an attacking element against a defending one comes only from `ElementChart.asset`; a pair it does not list deals x1. The chart refuses two elements with one id. No code file names an element, and the only multipliers in code are the 0 of immunity and the 1 of a pair that is not listed.
- **4b-AC2 Damage.** On the tick a shot lands, its target loses the shot's damage times the multiplier of the shot's element against the target's. A shot keeps the damage and the element its tower had at launch, also when the tower is sold in flight.
- **4b-AC3 Immunity.** A shot does no damage to an enemy of its own element, whatever the chart holds; the chart refuses a pair of one element and a multiplier at or below 0. A tower or an enemy with no element deals and takes x1.
- **4b-AC4 Targeting is unchanged.** A tower launches at the enemy in range nearest the exit (2c-AC2), whatever the elements. A shot at an immune enemy flies and lands like any other: the health does not change, nothing is paid, and the tower has fired for the refund (3b-AC5).
- **4b-AC5 Elemental enemies.** Every enemy has the element of its enemy definition from its entry until it leaves, also on a reused object. Enemies of different elements share the board and each keeps its own.
- **4b-AC6 Choosing.** The placement input holds the buyable towers in order: Light, then Shadow. Exactly one is selected, the first at the start of every run. Number key N selects the N-th tower, and so does a click on its button. A click on a cell with no tower tries to buy the selected tower (3b-AC2). Selling is unchanged.
- **4b-AC7 Buy bar.** The screen shows one button per buyable tower, in order, with its key, its element's display name, its cost and its element's colour. The selected button is marked, from the frame after the selection changes. The wording, the place, the sizes and the two states are those of the visual direction.
- **4b-AC8 The UI takes its clicks.** While the pointer is over an element that takes clicks (a button, the end screen), the board has no hover cell, the range disc is hidden, and a press neither buys nor sells. Labels take no click: the cells under them work as before.
- **4b-AC9 Colours.** A tower, an enemy and a shot are drawn with their element's material from the palette. A pooled view shows the element of what it draws now and changes material only when that element changes. The colours of both elements, of the hover and of the range disc are those of the visual direction.
- **4b-AC10 Announcement.** While a countdown runs, the screen names the elements of the wave to come with the seconds left; once a wave has started it names those of the wave that started last with the wave count. The names are the display names of the distinct elements of that wave's groups, in group order. The wording, the place and the style, colour included, are those of the visual direction. The rest of 3a-AC8 holds.
- **4b-AC11 Data.** The chart, both towers, every wave's enemy asset and the run equal the balance document. The two towers differ only in their element. Every tower and enemy asset under `Data/Assets/` has an element that is in the chart and in the palette; `Tower_Prototype` and `Enemy_Prototype` are gone. This replaces 2c-AC8 and 4a-AC5.
- **4b-AC12 Design target.** 3b-AC9 holds with the runs of the balance document for this step, whose reference order names a tower for each purchase. This replaces 4a-AC6 and retires 2c-AC9, whose neutral tower and enemy no longer exist.
- **4b-AC13 Core, layers and determinism.** Elements, the chart and immunity are rules of `ElementalTD.Core`, which still compiles with `noEngineReferences: true`. No code file under `Core/`, `Data/` or `Gameplay/` mentions a material or a colour. 3b-AC10 holds.
- **4b-AC14 Allocation.** 3b-AC11 holds with elemental hits. The view code that swaps materials and the hover with its UI check allocate 0 bytes per frame after warm-up. The HUD builds a text only when it changes.
- **4b-AC15 WebGL and regression.** As 4a-AC9, for this step.

## What the visual direction fills in
The assets task of each step applies these values; none needs code.

| Value | Where it lives |
|---|---|
| Colours of towers, enemies and shots per element, of the hover and of the range disc | Material assets, one per colour (ADR 0002) |
| UI colour per element | `ElementPalette.asset` |
| Place, size and style of the labels, the message line, the tower bar and its buttons in both states | `Hud.uss`, and the order of the elements in `Hud.uxml` |
| Wording | Text fields in the scene. Survived line: wave number, bonus. Tower button: key, element name, cost. Countdown: seconds, element names. Wave line: waves started, waves in the run, element names. One element name: display name, UI colour as hex. The separator between two names. |
| How long a message stays | `duration` on the message view |
| The margin around the board | `framePaddingCells` on the camera framer, one value for all sides |

A look that needs more than these comes back to the `tech-lead` for a task before the assets task starts: a margin per side, a survived line with a place of its own, a hover or a range disc that follows the selected tower, a shape or a prefab per element (ADR 0004), an icon in place of a name.

## Editor sequencing
As in plan 0004 and `docs/unity-cli-notes.md`, with the orchestrator's batch lock of plan 0005. The same five orders run once per step; 4b's code starts from 4a's tested code.

| Order | Work | Editor on the worktree |
|---|---|---|
| 1 | The code tasks of the step in parallel (separate files, signatures below), or one agent in their order. The `balance-designer` task in parallel: the port works from the rules of this plan | Closed. One batch EditMode run as the compile check: tests that read the real assets and those under "Tests that change" fail until orders 3 and 4; no other may. |
| 2 | Stage 4 review. The `qa-tester` writes the EditMode tests that need no asset | Closed. |
| 3 | The assets task, once the balance document and the visual direction are validated | Open, then quit with a save. |
| 4 | Stage 5 on both suites | Closed. |
| 5 | The build task and the browser check | Closed. |

## Tasks – 4a

### T4a-1 – Core and Data: wave groups and the bonus
Serves 4a-AC1 to AC3, AC5, AC7 and AC8. Code only.

```csharp
public readonly struct WaveGroup                             // today's checks on the count and the interval
{
    public readonly EnemyStats Enemy; public readonly int Count; public readonly float SpawnIntervalSeconds;
    public WaveGroup(in EnemyStats enemy, int count, float spawnIntervalSeconds);
}

public readonly struct Wave
{
    public readonly float DelayBeforeSeconds;
    public readonly int SurvivalBonus;                       // gold; rejects a value below 0
    public int GroupCount { get; }
    public WaveGroup GroupAt(int index);                     // in spawn order
    public Wave(WaveGroup[] groups, float delayBeforeSeconds, int survivalBonus = 0);   // copies the array; rejects null and empty
    public Wave(in EnemyStats enemy, int count, float spawnIntervalSeconds, float delayBeforeSeconds, int survivalBonus = 0);   // one group
}

// WaveSchedule: public Wave WaveAt(int index);
//               public int EnteredWaves { get; }            // waves whose last enemy has entered
// Enemy:        public int WaveIndex { get; private set; }  // set by Enter; -1 outside a wave
// Board:        public Enemy SpawnEnemy(in EnemyStats stats, int waveIndex = -1);
// Battle:       public int SurvivedWaves { get; private set; }      // waves that have paid
//               public int LastSurvivedWave { get; private set; }   // its number, from 1; 0 before the first
```

- `Wave.Enemy`, `Count` and `SpawnIntervalSeconds` go. `WaveSchedule` refuses a wave whose group count is not 1, in place of its check on the count, and reads group 0.
- `Battle`: an enemy enters with the index `Waves.StartedWaves - 1`. A new step after the projectiles: with a life left, each wave below `Waves.EnteredWaves` that has not paid and has no enemy of its index on the board pays its bonus, in wave order, and updates the two counters. The paid marks are one `bool[]` made in the constructor.
- `RunDefinition.cs`: a wave entry holds `delayBeforeSeconds`, `survivalBonus` (at least 0) and `groups`, each with `enemy`, `count` and `spawnIntervalSeconds`. `ToWaves()` also throws for a wave with no group.
- Tests, to compile only: the eight reads of `Enemy`, `Count` and `SpawnIntervalSeconds` on a wave in `RealAssetRunTests` and `RunDefinitionTests` become `GroupAt(0)`.

**Done when:** Core has no `UnityEngine`, no `float` touches the gold, and `Tick()` has no `new` on a tick that pays.

### T4a-2 – Presentation: the survived line
Serves 4a-AC4 and AC8. Code and UI text files; in parallel with T4a-1.

- `PlacementMessageView.cs` becomes `MessageView.cs`, moved with its `.meta`; the label `placement-message` becomes `message` in `Hud.uxml`, `Hud.uss` and `SceneTestBase` (the name only).
- It gains a serialized bootstrap and `waveSurvivedText`, a format of the wave number and the bonus. Each frame it compares `Battle.SurvivedWaves` with what it last saw; on a change it shows the text for `LastSurvivedWave` and that wave's bonus, through the path a refusal takes. The battle joins its start-up check.

**Done when:** the view holds no rule and no number.

### T4a-3 – Balance document
Serves 4a-AC5 and AC6. `balance-designer`, in parallel. The port follows "Rules for the port" below and reproduces its pins. The document gives everything the tests pin: the bonus per wave, every value of 3b-AC8, what the starting gold buys, building nothing (outcome, tick, gold at the end) and a reference order that wins (ticks, cells, outcome, lives, gold left, first kill).

**Done when:** the orchestrator has validated the document.

### T4a-4 – Assets and scene
Serves 4a-AC4 and AC5. Editor open on the worktree; after T4a-1 to T4a-3 and once the visual direction is validated. Through `unity command`: `Run_Prototype.asset` rewritten in the new format with the balance document's waves and bonuses, enemy assets as in T3c-3; on `Hud`, the bootstrap and the visual direction's `waveSurvivedText`. In `Hud.uss`, the message line as the visual direction states. Save the scene.

**Done when:** clean compile with an empty console; in Play a run with no tower shows the survived line for wave 1 and the gold rises by its bonus; everything is saved and `.vscode/settings.json` is reverted.

### T4a-5 – WebGL build
Serves 4a-AC9. As T3b-5 of plan 0005.

## Tasks – 4b

### T4b-1 – Core: elements and the hit
Serves 4b-AC1 to AC5 and AC13. Code only.

```csharp
public readonly struct Element : IEquatable<Element>         // the default value is no element
{
    public readonly int Id;                                  // 0: no element; an element's id is at least 1
    public Element(int id);                                  // rejects an id below 0
    public bool IsNone { get; }                              // plus ==, !=, Equals and GetHashCode
}

public sealed class ElementChart
{
    public ElementChart(int elementCount);                   // ids 1 to elementCount
    // Rejects no element, an id outside the chart, attacker == defender and a multiplier at or below 0.
    public void Set(Element attacker, Element defender, float multiplier);
    // 0 for one same element; otherwise the value set, or 1: never set, no element, or an id outside the chart.
    public float Multiplier(Element attacker, Element defender);
}

// TowerStats: public readonly Element Element;   constructor (…, int cost = 0, Element element = default)
// EnemyStats: public readonly Element Element;   constructor (…, int goldReward = 0, Element element = default)
// Enemy:      public Element Element { get; private set; }      // set by Enter
// Projectile: public Element Element { get; private set; }      // its tower's, copied by Launch
// Battle:     constructor (…, int sellRefundPercent = 100, ElementChart elements = null)   // null: an empty chart
```

Where a shot lands, `StepProjectiles` reads `Multiplier(projectile.Element, target.Element)` once into a local and passes the damage times it to `TakeDamage`. That is the only use of the chart. `FindTarget` does not change.

**Done when:** Core has no `UnityEngine`, and the whole EditMode suite of 4a passes untouched.

### T4b-2 – Data and Gameplay: definitions, selection and the click rule
Serves 4b-AC1, AC5, AC6, AC8 and AC11. Code only.

- New `ElementDefinition.cs`: `id` (at least 1) and `displayName`; `Element ToElement()` and `DisplayName`.
- New `ElementChartDefinition.cs`: `elements`, and `relations` of `attacker`, `defender` and `damageMultiplier`. `ElementChart ToChart()` throws for a missing reference, two elements with one id and a relation whose element is not listed.
- `TowerDefinition.cs` and `EnemyDefinition.cs`: an `element` reference, passed by `ToStats()` (no element while unassigned); the tower exposes it as `Element`.
- `PrototypeBootstrap.cs`: a serialized `ElementChartDefinition`, required like the map and the run, converted and passed to `Battle`.
- `TowerPlacementInput.cs`:

```csharp
[SerializeField] private TowerDefinition[] towers;           // replaces `tower`
[SerializeField] private UIDocument document;
public int TowerCount { get; }
public TowerDefinition TowerAt(int index);
public int SelectedIndex { get; private set; }               // 0 at start
public TowerDefinition Tower { get; }                        // the selected one: the tower a click buys
public void Select(int index);                               // ignores an index outside the list
```

  `Update` selects tower i when `Key.Digit1 + i` was pressed this frame. `TryPick` picks nothing while the list is empty or the document is missing, or when `panel.Pick` returns an element at the pointer; panel coordinates start at the top-left, so flip y before `RuntimePanelUtils.ScreenToPanel`.

**Done when:** the files hold no number other than neutral field defaults, and no material or colour.

### T4b-3 – Presentation: colours on the board
Serves 4b-AC9 and AC14. Code only; signatures of T4b-1 and T4b-2.

- New `ElementPalette.cs` (ScriptableObject): one look per element with the `ElementDefinition`, a UI `Color` and the tower, enemy and shot `Material`. It offers the looks by index and `bool TryGetLook(Element, out Look)`, a scan for start-up and clicks, never per frame.
- `BoardView.cs`: a serialized palette; a placed tower takes the material of `TowerOn(cell).Stats.Element`.
- `EnemyView.cs`: a serialized `body` renderer; `Show(position, healthFraction, material)` sets the material only when it is not the one last shown, compared by reference.
- `CombatView.cs`: a serialized palette. At start it builds the enemy and shot materials by element id, with the prefab's own material at 0 and for an id without a look. Per frame it reads them by id; a shot view keeps its renderer and the id it last showed.

**Done when:** no per-frame code allocates or assigns a material that is already shown.

### T4b-4 – Presentation: the buy bar and the announcement
Serves 4b-AC7, AC10 and AC14. Code and `Hud.uxml`; signatures of T4b-1 and T4b-2.

- `Hud.uxml`: an element `tower-bar`, ignored by picking. `Hud.uss` gets only what makes the bar and its buttons visible; T4b-6 styles them.
- New `TowerBarView.cs`: a serialized document, placement input, palette and `buttonText`, a format of the key, the element name and the cost. At start it adds one `Button` of class `tower-button` per tower, holding an element of class `tower-button__colour` with its look's UI colour as background and a label of class `tower-button__text`; a click calls `Select` with its index. Each frame it compares `SelectedIndex` with the button that has the class `tower-button--selected`.
- `RunHudView.cs`: a serialized palette and four formats: `waveElementsText` (waves started, waves in the run, names), `nextWaveText` with the names added, `elementNameText` (display name, UI colour as hex) and `elementSeparator`. The names of a wave come from `Waves.WaveAt(index)`: the distinct elements of its groups in order. The wave to come is `WaveAt(StartedWaves)`, the one that started last `WaveAt(StartedWaves - 1)`; before the first wave the wave line keeps `waveText`. The names are rebuilt when `StartedWaves` changes.

**Done when:** the views hold no rule, no number, no colour and no text of their own.

### T4b-5 – Balance document
Serves 4b-AC11 and AC12. `balance-designer`, in parallel; it can start during 4a. As T4a-3, plus: the chart's multipliers, the element of each wave, both towers, a reference order that names the tower of every purchase, and enemy assets named `Enemy_<Element><Health>`.

### T4b-6 – Assets, looks and scene
Serves 4b-AC6 to AC11. Editor open on the worktree; after T4b-1 to T4b-5 and once the visual direction is validated. Through `unity command`, except the two UI text files:
- `Data/Assets/`: `Element_Light.asset` (id 1) and `Element_Shadow.asset` (id 2); `ElementChart.asset` with both and the balance document's multipliers. `Tower_Prototype` renamed `Tower_Light` and copied to `Tower_Shadow`, each with its element. One enemy asset per wave as the balance document names them, each with its element, renamed from today's where possible; none left that no wave uses. `Run_Prototype.asset` with the document's values.
- `Presentation/Materials/`: `Tower`, `Enemy` and `Projectile` renamed `…_Light` and copied to `…_Shadow`, with the visual direction's colours; the hover and range disc materials as it states.
- `Presentation/ElementPalette.asset`: both looks, with the visual direction's UI colours. `Enemy.prefab`: `body` is the Body renderer.
- `Hud.uss` and `Hud.uxml`: the layout and the styles of the visual direction.
- `Prototype.unity`: the chart on the bootstrap; both towers and the `UIDocument` on the placement input; the palette on the board view, the combat view and the run HUD; a `TowerBarView` on `Hud`; every text field with the visual direction's wording. Save the scene.

**Done when:** clean compile with an empty console; in Play both buttons show, keys 1 and 2 and a click on a button move the mark, towers, enemies and shots take the colour of their element, and the HUD names the element of the wave to come and of the wave that started last; everything is saved and `.vscode/settings.json` is reverted.

### T4b-7 – WebGL build
Serves 4b-AC15. As T3b-5 of plan 0005.

## Rules for the port
The balance document is simulated on the Python port (`sim3c/core.py`), which follows plans 0005 and 0006 and these rules.

**From 4a**
1. Each enemy keeps the index of the wave it entered with.
2. A tick runs waves, enemies, towers, projectiles, wave bonuses, outcome.
3. Wave bonuses: nothing if no life is left. Otherwise, in wave order, each wave that has not paid, whose last enemy has entered (this tick included) and with none of its enemies on the board adds its bonus, a whole number, to the gold.
4. A wave that leaked entirely pays. The last wave pays on the tick that wins. The tick that loses pays nothing.
5. Pin, while the lives, the counts and the delays of waves 1 to 3 stay as in 3c: building nothing, wave 1 pays on tick 1672 and wave 2 on tick 2332, each give or take one; the run is lost on tick 2931 with the starting gold plus those two bonuses.

**From 4b**
6. Towers and enemies have an element; a shot copies its tower's element and damage at launch.
7. Targeting does not change: the element plays no part in it.
8. On landing, health drops by damage times the multiplier, in float32: 0 when the shot and the target have the same element, otherwise the chart's value for attacker against defender, otherwise 1. A hit of 0 kills nothing and pays nothing.
9. A launch at an immune enemy is a launch: the cooldown runs and the tower has fired for the refund.
10. Pin: a tower of damage 10 against the other element at x2 kills an enemy of 45 health with its third hit; against its own element the health stays 45 and the tower has fired.

## Tests for the qa-tester
On the worktree with its editor closed. A test not listed under "Tests that change" that fails is reported before it is changed.

### 4a
**Tests that change**
- `RunDefinitionTests`: the helper writes groups; add the bonus and a wave with no group.
- `RealAssetRunTests`, `SceneTestBase.ReferenceOrder` and the `RunSceneTests` that replay it: the document's values, bonuses included, as in plan 0005. Building nothing ends with the gold the document states, no longer the starting gold.
- `RunSceneTests` after a defeat (3b-AC6, 3a-AC5): the gold now includes the bonuses paid before the end, and a check that no message shows must come after the survived line has timed out.

**EditMode**, on `Battle`, with stats and bonuses chosen per test
- **4a-AC1:** a wave built from one group equals one built by the short constructor; null or empty groups and a negative bonus throw; `WaveSchedule` refuses a wave of two groups and a default wave; `WaveAt` returns each wave; `EnteredWaves` rises on the tick a wave's last enemy enters.
- **4a-AC2, AC3:**
  - Nothing built, lives to spare: the gold is unchanged until the tick the wave's last enemy exits, and rises by the bonus on that tick; `SurvivedWaves` is 1 and `LastSurvivedWave` 1.
  - A wave whose last enemy is killed pays on the tick of that kill, with the reward.
  - A long spawn interval empties the board between two enemies of one wave: nothing is paid until the last one has left.
  - A fast second wave leaves before a slow first one: wave 2 pays first, then wave 1, each once. Two waves that clear on one tick both pay on it.
  - With one life, the wave's only enemy exits: lost, gold unchanged, `SurvivedWaves` 0. With two lives it pays.
  - The last wave pays on the tick the outcome becomes `Won`; later `Tick()` calls change nothing.
  - An enemy spawned through `Board.SpawnEnemy` alone delays no bonus; an enemy object reused by a later wave does not hold back its earlier wave; a bonus of 0 is survived and pays nothing.
- **4a-AC5, AC6:** as the 3b-AC8 and AC9 tests, on the document, with the pin of rule 5.
- **4a-AC7, AC8:** the determinism test compares the gold after every tick of a run with bonuses; `Is.Not.AllocatingGCMemory` over ticks that pay.

**PlayMode**, in `Prototype.unity`
- **4a-AC4:** with nothing built, tick until `SurvivedWaves` is 1: on the next frame the line reads the visual direction's survived text for wave 1 with the asset's bonus, and the gold label has risen by that bonus; it hides after its duration; a refusal shown meanwhile replaces it.
- **4a-AC9:** the scene tests of 2b to 3c pass with the changes above.

**Orchestrator in the browser, then Eduardo**
- The line appears as the last enemy of a wave dies or exits, also for a wave that leaked, and the gold jumps with it.
- Is the line noticed? Does it ever hide a refusal the player needed, or the other way round?
- A run with no tower still ends in "Defeat"; a sensible build wins and feels less tight than 3c.

### 4b
**Tests that change**
- `BattleTests`: the 2c-AC8 test becomes part of the 4b-AC11 test; the two 2c-AC9 tests go. `MapDefinitionTests`: the speed test reads wave 1's enemy asset.
- `RealAssetRunTests` and `SceneTestBase.ReferenceOrder`: the battle gets the chart; each purchase names its tower, selected before the click; the document's values.
- Scene tests that need a tower to damage a wave enemy (2c-AC5, 3c-AC6, the first kill of `GoldSceneTests`) select the tower of the other element and expect the damage times the multiplier.
- `RunSceneTests`: the wave and countdown texts of 4b-AC10.
- Every EditMode test that builds its stats in code passes untouched: no element deals x1.

**EditMode**
- **4b-AC1:** the default `Element` is none; ids compare; a negative id throws. A chart answers 1 for a pair never set, the value set for the ordered pair only, also 0.25 and 0.75, and 1 with no element on either side. `ToChart()` holds the asset's rows and throws on two elements with one id. `ToStats()` passes the element of a tower and of an enemy.
- **4b-AC2:** with x2, a shot of 10 takes an enemy of 45 to 25 and the third kills it; with 0.25 a hit takes 2.5. A shot in flight when its tower is sold lands with that tower's element.
- **4b-AC3, AC4:**
  - A Light and a Shadow enemy in range of a Light tower, the Light one nearer the exit: every shot goes to the Light one, its health stays at the maximum after several landings, the Shadow one is untouched and the gold does not change.
  - After its first launch at an immune enemy the tower is sold for the refund percent.
  - When the immune enemy leaves the range the tower shoots the other one. An immune enemy reaches the exit and costs a life.
  - Same element is 0 in an empty chart; `Set` refuses a pair of one element, 0, a negative value, no element and an id outside the chart.
- **4b-AC5:** two overlapping waves of different elements: each enemy has its own; an object reused by a wave of the other element takes the new one, and a shot fired at its earlier entry is dropped (3a-AC10).
- **4b-AC11, AC12:** the assets hold the document's values; the towers differ only in element; every tower and enemy asset under `Data/Assets/` has an element that the chart holds; each run of the document, with the pin of rule 10.
- **4b-AC13:** source tests: no code file outside `Tests/` contains "Light" or "Shadow"; none under `Core/`, `Data/` or `Gameplay/` contains "Material" or "Color". The determinism test runs with both elements.
- **4b-AC14:** `Is.Not.AllocatingGCMemory` over ticks with hits of both kinds.

**PlayMode**, in `Prototype.unity`
- **4b-AC6, AC7:** two buttons hold the visual direction's text for Light and for Shadow, with the assets' cost, and their look's colour; the first is marked. A UI Toolkit click on the second, as the Restart test sends one, and key 2 through a queued keyboard state with the input enabled, each select it and move the mark; a click on a free cell then places a tower with the Shadow material. After a restart the first is selected.
- **4b-AC8:** `Hover` and `Click` at the screen position of a button's centre give no hover cell and no tower, though a cell lies under it or beside it; just outside the button they work. Over a label that covers a cell, if the layout has one, a click still buys.
- **4b-AC9:** the six element materials, the hover and range disc materials and the palette's UI colours hold the visual direction's values, and the palette has a look for every element of the chart. A tower of each element has its palette material. While waves of both elements overlap, every active enemy view has the material of the enemy it shows, on every frame; a shot view has its tower's. The 2a-AC4 test still finds only Presentation materials.
- **4b-AC10:** frame by frame over a run, the countdown and the wave line read the visual direction's texts, with the element of the wave to come and of the wave that started last; before the first wave the wave line names none.
- **4b-AC14:** the hover allocation test passes with the UI check.
- **4b-AC15:** the scene tests of 2b to 4a pass with the changes above.

**Orchestrator in the browser, then Eduardo**
- The screen matches the visual direction. The two elements are told apart at a glance, on towers, enemies and shots, and from the walls, the spawn and the exit; a shot can be seen in flight.
- The announced element is noticed in time to buy for it; the names on the buttons and in the announcement are read as the same thing.
- A click on a button never buys or sells under it, and no cell is out of reach behind the bar. Keys 1 and 2 work after a click on the page.
- A tower shooting an immune enemy visibly achieves nothing. Is it understood without a cue?
- The run plays as the document says; selling, the survived line, the end screen and the restart work as in 4a, and the browser console shows no error.

## Risks
- **Two sets of numbers.** Each step is balanced, pinned by tests and replayed by scene tests on its own document version. If 4a's version only adds the bonuses to the 3c numbers, its reference order stays valid and only the gold changes.
- **The port and Core are two implementations**, as in plan 0006; the pins of rules 5 and 10 tie both to this plan. If a design target fails by a small margin, the criterion is corrected, not the code.
- **Wasted shots.** With waves 8 s apart the board holds both elements most of the time, and a tower whose nearest target is immune does nothing. It is the rule; the document must be simulated with it, and the cue is parked.
- **One message line.** A survived line and a refusal replace each other. The last wave's line shows under the end screen.
- **The UI check.** `panel.Pick` runs on every hover; if it allocates, the hover test fails and the check moves to the bounds of the bar's buttons. A batch run has no focused window, so real mouse clicks on buttons are checked in the editor and in the browser only.
- **Buttons over the board.** A cell under an element that takes clicks takes none (4b-AC8). The visual direction must keep the bar off the cells at the sizes the game is played in.
- **Three documents in parallel.** The visual direction is written while this plan is. If it needs more than the table above offers, a task is added before the assets task; the code tasks do not wait for it.
- **Ids by hand.** Two elements with one id would be immune to each other; the chart refuses them, and the asset test checks every tower and enemy.
- **Optional parameters.** A production call that forgets the chart or a bonus gives x1 or no bonus with no error. Each has one call site, covered by the data tests.
- **The run asset changes format.** Its waves are empty from T4a-1 until T4a-4 rewrites them, and the scene does not start in between.
- **A renamed script.** `MessageView` keeps its `.meta`, so the scene keeps its reference; the editor refreshes the stored class name on the next save.
- **The range disc** is sized once, from the first tower. Both towers have one range; when ranges differ it must follow the selection.
- **Scene tests on real numbers**, as in plan 0005.
- **WebGL performance and GC.** Per tick: at most waves times enemies integer comparisons, and one array read per hit. Per frame: one integer comparison per pooled view. Texts and materials change on events, not per frame.
- **Coupling.** The placement input holds the `UIDocument`; three views hold the palette; `MessageView` now reads the battle.
