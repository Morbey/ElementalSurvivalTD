# Technical plan 0011 – Prototype slice 5, step 5a

Kills give XP, and the HUD shows the level and a bar that fills. Architecture: `docs/adr/0004-run-waves-outcome-restart.md`, amended for XP and the level. Numbers: `docs/balance/0001-prototype-economy.md` ("the balance document"), part "Step 5a: XP and level". Looks: `docs/art/visual-direction.md` ("the visual direction"), section 1.6. Prototype mode: this plan carries the acceptance criteria. It builds on plan 0010 and reuses "Editor sequencing" of plan 0007.

**What Eduardo sees:** a thin bar over the gold that fills by a third with each kill of wave 1; on the third kill the bar flashes, `Level 1` becomes `Level 2` and the bar starts again. A run that kills everything ends at `Level 8`. Nothing else changes: no choice, no pause, the same run.

## Approach
**XP and the level are counters in a Core class of their own.** `Progression` holds the run's XP, its level and the level table, and `Battle` owns one as it owns the wave schedule. A kill pays its XP where it pays its gold, and the level rises on that kill. Nothing reads them but the HUD, so no outcome, gold or tick of step 4e moves. `Battle` takes the progression as an optional parameter; without one it makes an empty one (level 1, no table), so every hand-built battle of the tests is untouched and no view checks for null.

**Order within a tick.** A kill pays gold, then XP, and the level rises there, inside the projectile step; the wave bonuses and the outcome follow, as today. In 5a this order cannot be observed. For 5b it fixes the rule: the choice is judged at the end of the tick, after the outcome, from the levels gained and not yet answered, so the bonus of a kill that both ends a wave and gains a level is already paid when the battle stops.

**The row is one small view.** `XpBarView` polls the progression after the frame's ticks and writes the label only when the level changes and the fill's scale only when the share it shows changes. The fill moves by `scale`, never by a width, so a kill costs no layout. The ease, the pop and the settle are USS: the base rules carry the transitions and one class, held for one frame, removes them and sets the pop look.

Discarded: XP and the level as fields of `Battle` (they would join combat, waves and gold, and 5b adds the offers there); the curve as a formula (ADR 0004); the row inside `RunHudView` (it would gain the pop's state; a view of 189 lines that owns the end screen); a `Progression` that `Battle` may lack (a null check in every reader). Nothing here is thrown away in 5b: its `Progression` gains the offers and the draw as further constructor parameters, and the pause reads the level this step raises.

## Acceptance criteria
"The table" is the level table: its n-th item is the XP from level n to n + 1.

- **5a-AC1 XP from kills.** XP is a whole running total that starts at 0 and never falls. A kill adds the killed enemy's XP reward on the tick its shot lands, once, where its gold reward is paid; the kill that wins the run counts. An enemy that exits and a shot dropped because its target left add none.
- **5a-AC2 Level.** The level starts at 1 and rises on the tick the XP gathered since it began reaches its item of the table; what is over carries into the next level, and one kill may gain several levels. Once the table has ended no level is gained and XP still counts. The progression answers the XP, the level, the XP into the level, the level's item and whether the table has ended. It refuses an item below 1.
- **5a-AC3 Nothing else changes.** Damage, targeting, gold, lives, waves, the tick order and the outcome are those of 4e: every EditMode and PlayMode test of 4e passes untouched, and the real run with the table equals the real run without it on every tick. A battle given no progression stays at level 1 and counts XP.
- **5a-AC4 Data and pins.** An enemy asset holds its XP reward and the run asset the table, and both equal the balance document: 10 on each of the 13 enemy assets; `30, 40, 70, 90, 110, 130, 150, 170, 190, 210`. The scene's battle has that table. On the real assets, the reference order gains its levels on ticks 1452, 2209, 3369, 4329, 5567, 7233 and 8873, with XP 30, 70, 140, 230, 340, 470 and 620 after them, and wins on tick 10317 with 75 kills, XP 750 and level 8; building nothing loses on tick 2931 with XP 0 and level 1. These come from the port: if Core disagrees, it is reported, not re-pinned.
- **5a-AC5 The row.** The bottom bar is a column of the direction's XP row and one row with the gold and the message line; sizes, colours and places are those of section 1.6 and of "Bottom bar" in 1.3. The bottom bar keeps its height, and the board area, the camera and the size of a cell are those of 4e at every aspect.
- **5a-AC6 The row follows the battle.** From the first frame the label reads the direction's text for level 1 and the fill's scale is 0 on x. On the first frame after a tick, the label shows the level and the fill's scale on x is the XP into the level over the level's item; a frame that ran several ticks shows the last state. Under the end screen the row stays as the last tick left it; after a restart it starts again.
- **5a-AC7 The ease and the pop.** A gain of XP eases the fill as the direction states; a gain on the frame right after a level takes its share at once, and the next gain eases. On a frame where the level differs from the one shown, except the first frame of a run, the row holds the pop look, with no transition: the label's scale, the track's colour, and the fill at its new scale at once. On the next frame where the level is the one shown, the pop look goes and the label and the track ease back as the direction states. Several levels gained before one frame play one pop, with the last level.
- **5a-AC8 The table ended.** Once the table has ended the label reads the direction's text with the level, the fill is at scale 1 in the direction's colour, and neither changes again, whatever XP comes.
- **5a-AC9 Layers, determinism and cost.** `Progression` is in `ElementalTD.Core`, with no `UnityEngine`. The view holds no rule, number, colour or text of its own. No file outside Core and the tests adds XP. `Tick()` allocates nothing, also on a tick with a kill and on one that gains a level. The view allocates nothing on a frame where the level is unchanged, and on a frame where only XP changes no element's layout rectangle changes. 3b-AC10 holds with the XP and the level compared after every tick.
- **5a-AC10 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari. Everything of 2b to 4e still holds.

**Read of the direction, for the art-director to confirm in its section 1.6.**
1. The row under the XP row is 44 high (72 less the XP row's 8 and 20), so the gold's centre is at 50. The message line stays beside the gold, as built.
2. Nothing separates the label from the track but the label's minimum width, 108 (settled by the orchestrator after this reading): it holds `Level 10` at the popped scale of 1.5 (103 measured) and `Max level 11` unpopped (100), so the track never moves and a popped level never lies over it; the pop of the level that ends the table lies over the grey full fill.
3. The level that ends the table pops like any other; "no more pops" means none after it. The grey fill at scale 1 covers the track's flash, so only the label is seen to pop.
4. The first frame of a run shows level 1 and an empty track with no pop and no ease.
5. The ease and the settle run on the panel's clock, as the sell value's drop does: real time, not battle time.

Not in this step (5b): the pause and the choice, the perks and the modifier system, the level line on the end screen, the tail on the tower panel, the message line counted in battle time, section 1.7.

**For 5b.** Open design question: the pop plays under the level-up overlay. Two cautions: perks reach `Battle` through `Progression` or the modifier system, not through another optional constructor parameter; and a kill on the tick that loses the run still pays XP, so a level gained there must open no choice.

## Tasks
Order and editor use: plan 0007, "Editor sequencing", with `tools/unity-lock.sh` around every batch run. T5a-1 and T5a-2 run in parallel on the signatures below. No balance task: the document and the port are done.

### T5a-1 – Core, Data and Gameplay: XP and the level
Serves 5a-AC1 to AC4 and AC9. Code only. New: `Core/Progression.cs`. Changed: `EnemyStats.cs`, `Enemy.cs`, `Battle.cs`, `Data/EnemyDefinition.cs`, `Data/RunDefinition.cs`, `Gameplay/PrototypeBootstrap.cs`.

```csharp
// Core
public sealed class Progression
{
    public Progression(int[] xpPerLevel);     // copied; null or empty: no level is ever gained; throws for an item below 1
    public int Xp { get; }                    // the running total
    public int Level { get; }                 // from 1
    public bool TableEnded { get; }
    public int XpIntoLevel { get; }           // gathered since the level began
    public int XpForLevel { get; }            // the level's item; 0 once the table has ended
    public void AddXp(int amount);            // raises the level while the item is reached; no allocation; refuses a negative amount
}
// EnemyStats: public readonly int XpReward;  // the last constructor parameter, default 0, refused below 0
// Enemy:      public int XpReward { get; private set; }   // taken on entry, as GoldReward
// Battle:     public Progression Progression { get; }     // never null; the last constructor parameter, default null
//             on a kill, after the gold: Progression.AddXp(target.XpReward);

// Data
// EnemyDefinition: [SerializeField, Min(0)] private int xpReward;            // passed to ToStats
// RunDefinition:   [SerializeField] private int[] xpPerLevel = new int[0];   // tooltip: item n is the XP from level n to n + 1
//                  public int[] ToLevelTable();   // a copy; throws, naming the run and the item, for an item below 1

// Gameplay
// PrototypeBootstrap: new Battle(..., elementChart.ToChart(), new Progression(run.ToLevelTable()))
```

One `Progression` belongs to one battle. `AddXp` is public for tests and simulations, as `Board.TryPlaceTower` is: outside Core only the battle's kill adds XP.

**Done when:** no other production file changes; Core has no `UnityEngine`; `AddXp` has no `new`; the EditMode suite of 4e passes untouched.

### T5a-2 – Presentation: the XP row
Serves 5a-AC5 to AC9. Code, UXML and USS by hand. New: `Presentation/XpBarView.cs`. Changed: `UI/Hud.uxml`, `UI/Hud.uss`.

- `Hud.uxml`: in `bottom-bar`, first `xp-row` holding `level-label` and `xp-track`, which holds `xp-fill`; then `economy-row` holding `gold-label` and `message`. Every new element ignores picking.
- `Hud.uss`: `#bottom-bar` becomes a column and `#economy-row` takes its row rules; the direction's sizes and colours; the transitions on the fill's scale, the label's scale and the track's colour in the base rules; `.xp-row--pop` with a transition time of 0 on all three, the label's scale and the track's colour; `.xp-row--max` with the fill's colour.
- `XpBarView`: fields for the document, the bootstrap and the two texts (`{0}`: the level). In `LateUpdate`: when the level is the one shown and the pop class is on, remove it; when the level differs, write the label (the max text once `TableEnded`), set the max class, and add the pop class unless this is the first write; when the share differs (the XP into the level over its item; 1 once `TableEnded`), write the fill's scale. The fill and the label are marked as dynamic transforms.

**Done when:** the view reads only `Battle.Progression`; no per-frame code builds a string or writes a style that did not change; no number or colour of the direction is in the script.

### T5a-3 – Assets and scene
Serves 5a-AC4 and AC5. Editor open on the worktree; after T5a-1 and T5a-2. Through `unity command`: `xpReward` on the 13 enemy assets and `xpPerLevel` on `Run_Prototype.asset`, both from the balance document; `XpBarView` on the `Hud` object with its references and the direction's two texts.

**Done when:** clean compile with an empty console; in Play the third kill of wave 1 turns `Level 1` into `Level 2` with the pop and the gold sits lower in its bar; everything is saved and `.vscode/settings.json` is reverted.

### T5a-4 – WebGL build
Serves 5a-AC10. As T3b-5 of plan 0005.

## Tests for the qa-tester
On the worktree with its editor closed. **No test of 4e is expected to change:** one that fails is reported before it is touched. Scene tests run with the time scale at 0 and tick by hand; they read the classes and the written styles, and the transitions from the resolved style, never a value half-way through an ease.

**EditMode**
- **5a-AC1:** on `Battle` with a tower and enemies of chosen XP: XP rises by the reward on the tick of the killing hit and on no other; two shots that land on one tick on a dying enemy pay once; an exit pays none; the kill that wins the run is counted.
- **5a-AC2**, on `Progression` alone, table `30, 40`: 29 XP is level 1 with 29 of 30; 30 is level 2 with 0 of 40; 35 is level 2 with 5 of 40; 70 is level 3, ended, item 0; one gain of 100 from 0 is level 3 with XP 100; a further gain leaves the level and raises the XP. An empty table and a null one: level 1, ended. An item of 0 is refused; the caller's array changed afterwards changes nothing.
- **5a-AC3:** the reference order on the real assets, once with the table and once without, compared after every tick: gold, lives, enemies, hits, shots, survived waves and the outcome.
- **5a-AC4:** every enemy asset and the run asset hold the document's numbers; `ToLevelTable` refuses an item below 1 and names it; both pins of the document, the level and the XP checked on each of the seven ticks and on the tick before each.
- **5a-AC9:** the determinism test of 4e-AC7 with XP and level in its snapshot; `TickAllocation` over the real run with the table: no tick allocates that did not in 4e, the seven level ticks included; source tests: Core has no `UnityEngine`, and no file outside Core and the tests contains `AddXp(`.

**PlayMode**, in `Prototype.unity`
- **5a-AC4:** the scene's `Battle.Progression` has 30 as the item of level 1.
- **5a-AC5:** the resolved sizes, colours, minimum width and transform origin of section 1.6; the bottom bar is 72 high; the gold's centre is 50 below the bar's top, within the snap; the message line is beside the gold and inside the bar; `AssertLayout` at the game view's aspect and at 1 and 2.5, and through a pop.
- **5a-AC6:** the first frame; after each of the first two kills the fill's written scale is a third and two thirds and the label is not rewritten: a sentinel text put on it survives, or the frame allocates nothing (`Label.text` keeps its old object when given an equal string, so "the same string object" proves nothing); ten ticks with no frame, then one frame, show the last state; after a defeat the row does not change over a second of real time; after a restart it is `Level 1` and 0.
- **5a-AC7:** on the frame after the third kill: the pop class, the new text, the fill's written scale 0 and its resolved scale 0, and resolved transition times of 0; on the next frame the class is gone and the three transitions are the direction's. No pop class on any frame before the third kill. Three levels fed through `AddXp` before one frame: one pop, with the last level; a level fed on the pop frame keeps the class one frame more.
- **5a-AC8:** not reachable on the real run, so fed through `AddXp` on the scene's progression: on the frame after the table ends, the max text with level 11, the max class, the written scale 1 and the fill's resolved colour; after more XP, a sentinel text and a sentinel scale put on the row survive, and no pop class.
- **5a-AC9:** `Allocations.Of` the view's `LateUpdate` over 300 ticks of wave 1 with kills: 0 on every frame but those that change the level; on frames where only XP changes no element under `bottom-bar` has a new layout rectangle or gets a `GeometryChangedEvent`.
- **5a-AC10:** the scene tests of 2b to 4e pass untouched.

**Mutations the tests must kill**
- Core: XP paid at launch, per hit or for an exit; `>` for `>=` at the item; the carry-over dropped; one level per kill at most; the table read as running totals, or one item off; the level raised at the end of the tick or on the next; XP no longer counted once the table ends.
- View: the fill as XP over the item without the level's start; the label written every frame; a pop on the first frame; the pop class never removed, or removed on the same frame; the fill moved by a width.

**Orchestrator in the browser, then Eduardo**
- Before the first kill: `Level 1`, an empty grey track across the bar, the gold and the message line readable under it; the bar's colour still matches the background; the board and its cells are where they were in 4e.
- Wave 1: each kill fills a third with a short ease; the third flashes the bar, the label jumps and settles on `Level 2`, the bar is empty again. Does the flash read as a level, and is the label's last digit readable during it (read 3 above)?
- Late waves: with kills in bursts the bar keeps up and never runs backwards except on a level.
- A run that kills everything gains seven levels, about one every 25 s, and ends at `Level 8` with the bar about three quarters full. Building nothing ends in "Defeat" at `Level 1`.
- Under the end screen the row stays; after Restart it is `Level 1` and empty.
- `Wave 3 survived +25` still fits beside the gold. The text is sharp during the pop, in Chrome and Safari, on the canvas and in full screen. The console shows no error.
- The look of an ended table cannot be reached in play: the tests cover it.

## Risks
- **Two implementations.** The pins of 5a-AC4 come from the port and Core has not run them. They meet on the seven ticks; a difference is a bug in one of the two.
- **The pop in one class.** The fill's inline write on the pop frame starts an ease under the base rule, because the class is not resolved yet; the fill is at its new share at once only because the engine cancels that ease in the same frame's style pass, where the class's transition time of 0 removes the transition. This is engine behaviour, not a CSS rule: its pin is the fill's resolved scale on the pop frame. The pop and settle classes of the sell value (4c) would not fix it, since an inline write always sees the style resolved on the frame before.
- **A scale write on a transition.** Read in the engine's reference source at the stage 4 review: it retargets from the current value with a fresh duration and allocates nothing, except possibly once on the panel's first scale transition. The 5a-AC9 scene test measures allocations and layout on those frames.
- **The gold moves 14 down.** No test pins its height, and the message line follows it; the browser check judges it.
- **Seven strings per run.** A label text is built on each level, on a frame that also draws the pop. Nothing else allocates.
- **Coupling.** `XpBarView` reads the bootstrap and `Battle.Progression`, and knows no other view. `Battle` gains one line on a kill.
- **Stacked on unaccepted steps.** 4b to 4e are tested and not accepted; a change asked there lands under this branch.
