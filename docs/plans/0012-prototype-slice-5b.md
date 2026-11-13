# Technical plan 0012 – Prototype slice 5, step 5b

A level-up pauses the battle and offers three perks; the one picked changes the towers through the modifier system. Architecture: `docs/adr/0007-modifiers-perk-choice-pause.md`. Numbers: `docs/balance/0001-prototype-economy.md` ("the balance document"), re-proposed for this step by T5b-6. Looks: `docs/art/visual-direction.md` ("the visual direction"), section 1.7 and the 5b items of 1.3 and 1.5. Prototype mode: this plan carries the acceptance criteria. It builds on plan 0011 and reuses "Editor sequencing" of plan 0007.

**What Eduardo sees:** on the third kill of wave 1 the board freezes under a dark screen, `Level 2`, and three cards deal in. He clicks `Light towers +25 % damage`; the screen goes, the battle goes on, and his Light towers now hit for 25 instead of 20. A selected tower reads `12.5` with a green `+2.5`. Seven choices in a run that kills everything, different on every restart.

**The cut.** What plays cannot be cut: the pause, the draw, the pick and the perks' effect move every outcome together, so any first step without one of them needs its own reference order and a second re-pin when the rest lands. Two criteria move no tick and can follow as a step 5c without touching a pin: 5b-AC12 (the tail) and 5b-AC14 (the level on the end screen). Recommended: keep them here; they are T5b-4's smallest parts, and a step of their own costs a test pass, a build and an acceptance for two labels.

## Rules for the port and their pins
The port follows the rules of plans 0007 and 0010 and these. Ticks count `Tick()` calls that ran; "float32 after each" means every sum and product is rounded to float32 before the next.

16. **A modifier** is a stat, an element or none, an operation (add or multiply), an amount and a source. Stats: tower damage, tower shots per second, tower range, gold per kill. With an element it applies to the towers of that element only; with none, to every tower.
17. **Effective value** = the base; then every addition on that stat, in the order the modifiers were added; then every multiplication, in the order added; float32 after each. Additions come first whatever the pick order: base 10 with a x1.5 added before a +2 is 18, not 17.
18. **A tower** keeps its base stats and its effective ones. The effective ones are computed when it is bought, with every modifier so far, and recomputed for every tower on a pick. Targeting reads the effective range; a launch copies the effective damage and adds `1 / effective shots per second` (float32) to the cooldown. A pick changes neither a shot in flight nor a cooldown already running. The purchase and the refund read the base cost.
19. **Gold per kill** is whole: a kill pays its reward plus every addition on gold per kill. Nothing else pays more.
20. **The generator.** State `s`, an unsigned 32-bit number, starts as the seed; all arithmetic is modulo 2^32, and `>>` shifts in zeros.
    - `Next()`: `s = s + 0x9E3779B9; z = s; z = z ^ (z >> 16); z = z * 0x85EBCA6B; z = z ^ (z >> 13); z = z * 0xC2B2AE35; z = z ^ (z >> 16);` return `z`.
    - `Below(n)`: `(Next() * n) >> 32`, the product taken in 64 bits: a whole number from 0 to n − 1.
21. **A draw**, for a pool of `n` perks named by their place in the run's list, from 0: `order = 0, 1, …, n − 1`, set afresh for every draw; for `i` = 0, 1, 2: `j = i + Below(n − i)`, swap `order[i]` and `order[j]`. The offer is `order[0], order[1], order[2]`: slots 0 to 2, left to right. Every draw takes three numbers; nothing is taken before the first choice.
22. **A choice** opens at the end of a tick, after the wave bonuses and the outcome, when the run is still running and a level was gained and not yet answered. It is drawn when it opens. While it is open `Tick()` does nothing and counts nothing. A level gained on the tick that ends the run opens no choice.
23. **A pick** names a slot: that perk's modifier is added with the perk's place as its source, every tower is recomputed, and the choice closes. If levels are still unanswered, the next choice opens at once, with its own draw. Choices answer the levels in rising order.
24. **Order at one tick count:** the pick comes before any action due there. A run that never picks stops for good on its first level-up tick.

**Generator pin.** Seed 1: `Next()` returns `96A0F96B`, `12BC8390`, `971E9964`. With a pool of 5 the first four draws are (2, 1, 3), (2, 0, 4), (4, 2, 1), (3, 2, 1); with seed 2, (3, 1, 2), (2, 3, 4), (2, 1, 4), (0, 1, 4). Seed 0 starts with `92CA2F0E`.

**Modifier pin.** Pool: 0 Light damage x1.25; 1 Shadow damage x1.25; 2 every tower's shots per second x1.15; 3 every tower's range +0.5; 4 gold per kill +2. A Light and a Shadow tower of range 2.5, damage 10 and 1.5 shots/s. After the picks 0, 2, 3, 0, 2:

| | Damage | Shots/s (float32 bits) | Range | Cooldown added per launch |
|---|---|---|---|---|
| Light | 15.625 | 1.9837499 (`3FFDEB84`) | 3.0 | 0.5040958 |
| Shadow | 10 | 1.9837499 | 3.0 | 0.5040958 |

After the first pick of perk 2 alone the rate is 1.7249999 (`3FDCCCCC`). A tower bought after the five picks has the same stats. A kill still pays its reward and no more.

**Pause pin.** The prototype map, 10 lives, 40 gold, refund 50, the chart and the Light tower of the balance document (cost 40, range 2.5, damage 10, 1.5 shots/s, projectile 10), level table `10, 10, 10`, seed 2, and a pool of three: 0 Light damage x1.25; 1 every tower's range +0.5; 2 gold per kill +2. Enemies are Shadow: speed 2, health 45, gold 10, XP 10. Wave 1: delay 0, bonus 7, one enemy. Wave 2: delay 2 s, bonus 9, two enemies 1 s apart. Before the first tick a Light tower is bought on (2,3).

| Tick | What happens |
|---|---|
| 1 | Enemy 1 enters; launches on 1, 40 and 80 land on 12, 49 and 95, each for 20. |
| 95 | The third hit kills: gold 0 + 10 + 7 = 17, wave 1 survived, XP 10, level 2. The choice opens with the offer (2, 1, 0). `SecondsToNextWave` reads 0.4167. |
| – | Any number of further `Tick()` calls: the tick count stays 95, the gold 17, the countdown 0.4167, the hit count 3. |
| – | Pick slot 2 (perk 0): the tower's damage is 12.5. The next `Tick()` is tick 96. |
| 121 | Enemy 2 enters; launches on 121 and 160 land on 132 and 169, each for 25. |
| 169 | The second hit kills: gold 27, XP 20, level 3, wave 2 not survived. The choice opens with the offer (1, 2, 0). Pick slot 1 (perk 2). |
| 181 | Enemy 3 enters; launches on 200 and 240 land on 208 and 251, each for 25. |
| 251 | The kill pays 10 + 2, wave 2 pays 9: gold 48. Won, 10 lives, XP 30, level 4, no choice open, two opened in the run. |

## Approach
**The modifier system is one list and a cache.** `Modifiers` holds the run's modifiers in the order added and answers an effective value by the fixed order of rule 17. A tower keeps its base stats and caches its effective ones, which the battle refreshes on a purchase and on a pick, so the tick still reads a struct and allocates nothing. Gold per kill is read from the list on a kill. A perk is an asset with one modifier and the words of its card; the pool is a list on the run asset. `Progression` gains the pool, the offers and the draw; the generator is a Core class of fifteen lines that the port copies.

**The pause is a state of the battle.** A battle awaiting a choice ignores `Tick()` as a finished one does, the driver gathers no time, and the placement input picks nothing. Every view that shows the simulation already ages in ticks, so the board, the shots and the damage numbers stand still with no code; the survived line moves to that clock. The screen is one view that polls the progression, writes three cards when a choice appears and calls `Battle.PickPerk` when the picked look has been held. Its timings are real time: the lock and the hold are scene fields, the dealing and the fades are USS.

**Tests and simulations must answer.** A battle with no pool never opens a choice, so every hand-built battle of the tests is untouched. The scene's battle has the pool: a scene test that ticks past a level-up meets a choice, and the base class answers it from the reference picks or fails.

Discarded: see ADR 0007 (`Time.timeScale`, stats computed when read, a list per tower, percent that adds up, `System.Random`, a linear congruential generator, the offers in their own class, a Core event, card words derived from the stat). Also: three fixed perks and no draw as a first step (the cut above); cards written in UXML three times (the view builds `Progression.OfferSize` of them, as the tower bar builds its buttons); a clamp with a bound nobody can reach (ADR 0007, decision 2); the tail's red look for a lowered value (no perk lowers).

## Acceptance criteria
- **5b-AC1 Modifiers.** Rules 16 and 17 and the modifier pin. A modifier that multiplies gold per kill, adds a fraction to it, or puts an element on a stat that is not a tower's is refused when it is made. Nothing removes a modifier.
- **5b-AC2 Towers.** Rules 18 and 19. `Tower.Stats` is the effective value and `Tower.BaseStats` the bought one. A tower of no element takes only the modifiers of no element. The sell value and the gold check of a purchase read the base cost. `Modifiers.Apply` answers the effective stats of any base stats and changes nothing.
- **5b-AC3 The draw.** Rules 20 and 21 and the generator pin. The three perks of an offer are distinct. Same seed and same picks give the same offers. A pool of no perk never opens a choice; a pool of one or two is refused.
- **5b-AC4 The pause.** Rules 22 and 24 and the pause pin. While a choice is open nothing of the battle moves: the tick count, the countdown, the cooldowns, the shots, the enemies, the hit log. A battle with no pool is the 5a battle on every tick.
- **5b-AC5 The pick.** Rule 23. `Battle.PickPerk` refuses, by throwing, when no choice is open or the slot is outside the offer. `Progression` answers, for the open choice, its level, its number among the choices opened in a row and how many those are, and for each perk how often it was taken. After a pick of a perk that touches no tower of the run, every later tick equals the run with no pool.
- **5b-AC6 Driver and input.** While a choice is open the driver runs no tick, gathers no time and its tick fraction is 0; the first tick after the pick runs on the next frame, with no burst. The placement input has no hover cell, buys, selects and deselects nothing, and its keys do nothing; a tower selected before stays selected. No production code writes `Time.timeScale`.
- **5b-AC7 Seed.** A scene run takes `PrototypeBootstrap.SeedForNextRun` when it is set, and clears it; otherwise a seed from the clock. A restart draws from a fresh seed.
- **5b-AC8 Data.** A perk asset holds one modifier (stat, element or none, operation, amount), its title and its attribute text. The run asset lists the pool. Refused, naming the asset: a pool of fewer than three, an empty place, a perk listed twice, an addition at or below 0, a multiplication at or below 1, and whatever 5b-AC1 refuses. The pool, the amounts, the waves and the enemy assets equal the balance document, and the scene's battle has that pool. This replaces the enemy assets and waves of 4e-AC5.
- **5b-AC9 Design target and pins.** On the real assets and the document's seed, the document's reference order with its picks meets every choice on its tick with its offer and ends as stated (tick, lives, gold, kills, XP, level, towers); building nothing loses as stated. No damage amount of the document reaches 100. These come from the port: if Core disagrees, it is reported, not re-pinned. This replaces 4e-AC6 and the ticks of 5a-AC4.
- **5b-AC10 The level-up screen.** From the first frame after the tick that opens a choice, the overlay of section 1.7 shows: the title with the choice's level, the hint (with `(1 of 2)` only for choices in a row), and three cards in slot order. A card shows, per the direction's table: the tile (the element's, `All` for a tower stat with no element, `Gold` for gold per kill), the perk's title with the element's name in its UI colour, the number from the modifier (`+25 %` for x1.25, `+0.5` for +0.5) and the perk's attribute text. A perk taken before shows the stack line with the total after this pick (`Total +56 %` for a second x1.25, `Total +1` for a second +0.5); a new one shows none. Sizes, colours and places are the direction's. The overlay lies above the floating texts and under the end screen, and hides on the frame the last choice is answered.
- **5b-AC11 Lock, pick and hold.** For the direction's lock after a choice appears, the second in a row too, a click on a card does nothing and the cards ignore picking. Then a click picks: the card holds the picked look and the others the passed look for the direction's hold, during which the choice is still open in Core and no click counts; then `PickPerk` is called, and the next choice deals in or the overlay hides. While the overlay shows, the buy buttons, Sell and the close button take no click. No key picks. The lock and the hold run in real time; the dealing, the title's entry and the fades are USS transitions.
- **5b-AC12 The tail.** A stat row of the tower panel shows the effective value and, when the shown value differs from the shown base, the direction's tail: values to three significant figures and the tail as their difference (`1.73` and `+0.23` for 1.5 x1.15; `15.6` and `+5.6`; `3` and `+0.5`). A row with no difference looks as in 4c.
- **5b-AC13 Effective values on screen.** The panel's values are effective and are rewritten, while it is open, on the first frame after a pick. The range disc shows the effective range of the selected tower, of the hovered tower and of the tower to buy. Sell's tail shows the base cost. A damage number shows the damage dealt (25 after one Light damage pick), and a frame whose hits show only amounts seen before allocates nothing.
- **5b-AC14 The end screen** shows the direction's level line with the run's level.
- **5b-AC15 The survived line** stays for its duration in battle time, from the frame that shows it: it does not run out while a choice is open, and is seen after the pick.
- **5b-AC16 Layers, determinism and cost.** `Rng`, `Modifier`, `Modifiers` and the offers are in `ElementalTD.Core`. No view holds a rule, number, colour or text of its own, or computes an effective value. `Tick()` allocates nothing, also on the tick that opens a choice and on a kill with gold perks. No view allocates or writes a style on a frame where what it shows did not change, the overlay included. 3b-AC10 holds with the offers, the picks and every tower's stats compared.
- **5b-AC17 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari. Everything of 2b to 5a that this plan does not replace still holds.

**Read of the direction, for the art-director to confirm.**
1. **Stacks compound**, so the stack line of a second `+25 %` reads `Total +56 %`, not the `+50 %` of 1.7's example: the product less one, as a whole percent, half up. An adding perk shows its sum.
2. The tile follows the modifier: an element, else `All` for a tower stat, else `Gold`. The element's initial is the first letter of its display name; its colour on the tile is a new colour per element in the palette (1.3: dark on Light and Air).
3. The pick reaches the battle when the hold ends, so the panel and the range disc under the overlay change as it goes, not on the click.
4. While a choice is open the hover and its disc hide; a selected tower keeps its frame, its panel and its disc under the overlay.
5. A tail pushes its value to the left by its own width; rows without one keep the 4c alignment. The `Element` row never has one.
6. The end screen reads `Level 11` for an ended table, not `Max level 11`, and counts a level gained on the last tick.
7. Rounding "half up" is done on the value as seven significant figures, so 1.5 x1.15, which a float holds as 1.7249999, shows `1.73` as the direction says.

Not in this step: critical hits; rarity, weights and luck in the draw; perks with a downside, clamps and the tail's red look; removing a modifier; modifiers on enemies; a seed shown to the player; two choices in a row in play (the level table makes them impossible; the tests reach them).

## Tasks
Order and editor use: plan 0007, "Editor sequencing", with `tools/unity-lock.sh` around every batch run. T5b-1, T5b-2 and T5b-3 run in parallel on the signatures below; T5b-4 follows T5b-3 with the same agent (both write `Hud.uxml` and `Hud.uss`); T5b-6 runs in parallel from now. Only T5b-5 needs the editor open.

### T5b-1 – Core: modifiers, the draw and the pause
Serves 5b-AC1 to AC5 and AC16. Code only. New: `Core/Rng.cs`, `Stat.cs`, `Operation.cs`, `Modifier.cs`, `Modifiers.cs`. Changed: `Progression.cs`, `Battle.cs`, `Tower.cs`.

```csharp
public sealed class Rng { public Rng(uint seed); public uint Next(); public int Below(int count); }   // rule 20; unchecked
public enum Stat { TowerDamage, TowerShotsPerSecond, TowerRange, GoldPerKill }   // serialized on assets by value: append only
public enum Operation { Add, Multiply }
public readonly struct Modifier
{
    public readonly Stat Stat; public readonly Element Element; public readonly Operation Operation;
    public readonly float Value; public readonly int Source;                     // -1: none
    public Modifier(Stat stat, Operation operation, float value, Element element = default, int source = -1);   // refusals of 5b-AC1
    public static bool IsTowerStat(Stat stat);
    public float Stacked(int times);              // this modifier applied that many times to 0 (add) or 1 (multiply), by rule 17
}
public sealed class Modifiers
{
    public int Count { get; }                     // modifiers added since the run began; a view polls it
    public Modifier At(int index);
    public void Add(in Modifier modifier);
    public float Apply(Stat stat, Element element, float value);      // rule 17; no allocation
    public int ApplyWhole(Stat stat, int value);                      // a whole stat: the value plus its additions
    public TowerStats Apply(in TowerStats stats);                     // damage, shots per second and range; the rest as given
}
// Tower:  public TowerStats BaseStats { get; }   public TowerStats Stats { get; internal set; }   // effective; the base until the battle sets it
// Progression
public const int OfferSize = 3;
public Progression(int[] xpPerLevel, Modifier[] perks = null, uint seed = 0);    // perks copied, each given its place as source
public int PerkCount { get; }
public Modifier PerkAt(int perk);
public int TimesTaken(int perk);
public bool ChoiceOpen { get; }
public int ChoicesOpened { get; }                 // since the run began; a view polls it
public int ChoiceLevel { get; }                   // the level the open choice answers
public int ChoiceNumber { get; }                  // from 1, among the choices opened in a row
public int ChoicesInARow { get; }
public int OfferAt(int slot);                     // the perk on the slot; throws with no choice open
internal void OpenChoice();                       // nothing when none is owed or one is open
internal int Pick(int slot);                      // the perk taken; opens the next choice when one is owed
// Battle
public Modifiers Modifiers { get; }               // never null; the battle makes it
public bool AwaitingChoice => Progression.ChoiceOpen;
public void PickPerk(int slot);                   // Pick, Modifiers.Add, every tower's Stats recomputed
// Tick(): returns at once while AwaitingChoice; its last line, when the run is still running: Progression.OpenChoice()
// TryBuyTower: on Placed, the new tower's Stats = Modifiers.Apply(stats).   TryGetSellValue: BaseStats.Cost
// a kill: Gold += Modifiers.ApplyWhole(Stat.GoldPerKill, target.GoldReward)
```

**Done when:** no constructor of `Battle` changes; the EditMode suite of 5a passes untouched; `Tick()`, `Apply`, `ApplyWhole` and a draw have no `new`; Core has no `UnityEngine` and no text.

### T5b-2 – Data and Gameplay: perks, the seed, the driver and the input
Serves 5b-AC6 to AC8. Code only. New: `Data/PerkDefinition.cs`. Changed: `Data/RunDefinition.cs`, `Gameplay/PrototypeBootstrap.cs`, `Gameplay/TowerPlacementInput.cs`.

```csharp
// PerkDefinition (menu "ElementalTD/Perk Definition", file "Perk_"): stat, element (unassigned: none), operation, value,
//   title ({0}: the element's name), attributeText
public string Title { get; }   public string AttributeText { get; }   public ElementDefinition Element { get; }
public Modifier ToModifier();                     // throws InvalidOperationException naming the asset (5b-AC8)
// RunDefinition: [SerializeField] private PerkDefinition[] perks = new PerkDefinition[0];
public PerkDefinition PerkAt(int index);
public Modifier[] ToPerks();                      // in order; throws naming the run and the place (5b-AC8)
// PrototypeBootstrap
public static uint? SeedForNextRun;               // for tests: taken and cleared in Awake
public RunDefinition Run => run;
// Awake: new Progression(run.ToLevelTable(), run.ToPerks(), SeedForNextRun ?? a seed from the clock)
// Update: returns while Battle.AwaitingChoice; a tick that opens a choice ends the frame's ticks and sets the accumulator to 0
// TowerPlacementInput: TryPick answers false and ReadKeys does nothing while Battle.AwaitingChoice
```

**Done when:** no other production file changes; Gameplay still instantiates nothing and names no view.

### T5b-3 – Presentation: the level-up screen
Serves 5b-AC10, AC11 and AC16. Code, UXML and USS by hand. New: `Presentation/LevelUpView.cs`, `Presentation/StatText.cs`. Changed: `ElementPalette.cs`, `UI/Hud.uxml`, `UI/Hud.uss`.

- `StatText`: a static helper with no text of its own. The cast of a float to `decimal` keeps seven significant figures; from it, a value half up to three significant figures, a multiplication as a whole percent half up, and the difference of two shown values. Used by this view and by the panel.
- `ElementPalette.Look`: `TileTextColor`, the initial's colour on the element's tile.
- `Hud.uxml`: between `floating-texts` and `end-screen`, `level-up` holding `level-up-title`, `level-up-hint` and the empty row `perk-cards`. `Hud.uss`: section 1.7. The overlay is hidden by `display`, takes clicks and covers the panel. Base rules carry the transitions; `.level-up--enter`, held for one frame, sets the title's and the cards' start look with a transition time of 0; `.perk-card--slot-1` and `--slot-2` carry the dealing delays; `.perk-card--picked` and `.perk-card--passed`, written after them, carry their looks with no delay; `.perk-card__tile--all` and `--gold`.
- `LevelUpView`: fields for the document, the bootstrap, the palette, the lock and the hold in seconds, and the texts (title, hint, hint in a row, percent, amount, stack line, the two tile words, an element name in its colour). In `Start` it builds `Progression.OfferSize` cards (a `Button` that is not focusable, its five labels ignoring picking) and the title, number and attribute text of every perk of the pool, once. In `LateUpdate`, after the frame's ticks: a pick whose hold has ended goes to `Battle.PickPerk`; a count of opened choices that differs from the one shown writes the title, the hint, each card's texts, tile and stack line, adds the enter class and starts the lock; the frame after, the enter class goes; when the lock ends the cards take picking; with no choice open the overlay hides. A card's click does nothing while the lock or a hold runs, whatever its picking mode; otherwise it sets the picked and passed classes and starts the hold.

**Done when:** the view reads only `Battle`, `Progression`, `Modifiers`, the run's perk assets and the palette; nothing is built or written on a frame where no choice appears, no lock ends and no pick is made; no number, colour or word of the direction is in the script.

### T5b-4 – Presentation: the views that follow
Serves 5b-AC12 to AC16. After T5b-3. Changed: `TowerPanelView.cs`, `BoardView.cs`, `MessageView.cs`, `RunHudView.cs`, `DamageNumberView.cs`, `Data/TowerDefinition.cs`, `UI/Hud.uxml`, `UI/Hud.uss`.

- `TowerPanelView`: the three values from `Stats` and `BaseStats` through `StatText`, each with a tail label (`stat-damage-tail`, `stat-shots-tail`, `stat-range-tail`; a new format field with the direction's signs) hidden when the difference is 0; rewritten when the tower or `Modifiers.Count` changes. Sell's tail reads `BaseStats.Cost`. (AC12, AC13)
- `BoardView`: the disc of the tower to buy reads `Battle.Modifiers.Apply(toBuy.ToStats()).Range`; `TowerDefinition.RangeCells` loses its last reader and goes. (AC13)
- `DamageNumberView`: the amount texts are pre-sized with the cap of numbers alive. (AC13)
- `RunHudView`: `end-level` under the title, written with a new text field when the outcome changes; the direction's margins in USS. (AC14)
- `MessageView`: the line hides when the battle has ticked its duration since the frame that showed it; the duration field's tooltip says battle time. (AC15)

**Done when:** no view compares a stat with its base outside `StatText`; the panel builds no string on a frame with the same tower and the same count.

### T5b-5 – Assets and scene
Serves 5b-AC8 to AC12 and AC14. Editor open on the worktree; after T5b-1 to T5b-4 and once the balance document is validated. Through `unity command`: the perk assets under `Data/Assets/` (`Perk_<Name>`) with the document's amounts and the direction's titles and attribute texts; `perks` on `Run_Prototype.asset` in the document's order; the waves and enemy assets where the document changes them; `TileTextColor` on the palette's looks; `LevelUpView` on the `Hud` object with its references, the direction's times and texts; the new text fields of `TowerPanelView` and `RunHudView`.

**Done when:** clean compile with an empty console; in Play the third kill of wave 1 opens the choice and a pick resumes the battle; everything is saved and `.vscode/settings.json` is reverted.

### T5b-6 – Balance (`balance-designer`, chained by the orchestrator)
Serves 5b-AC8 and AC9. The port takes rules 16 to 24 and reproduces the three pins above before anything is tuned. The document then gives everything the tests pin:
- the pool in order (a perk's place is its name in the draw) and each amount; gold amounts whole;
- the waves, retuned by whole hits, and the enemy assets that change;
- the pick policy of each bot;
- the reference seed, and the reference order with a row per choice: the tick it opens on, the offer as three places, the place picked; then its outcome (tick, lives, gold, kills, XP, level, towers) and building nothing;
- the largest damage amount any run can show, which must stay below 100 (three digits return to the art-director), and that no run gains two levels on one tick.

### T5b-7 – WebGL build
Serves 5b-AC17. As T3b-5 of plan 0005.

## Tests for the qa-tester
On the worktree with its editor closed. Scene tests run with the time scale at 0 and tick by hand; the lock and the hold are real time, so a test waits them out in real seconds; transitions are read from the resolved style, never half-way.

**`SceneTestBase`.** `SetUp` sets `SeedForNextRun` to the reference seed before the scene loads. One `Tick()` helper replaces every direct `Battle.Tick()` of the scene tests: it fails, naming the tick, when a choice is open, then ticks. The reference picks sit beside the reference order; after a tick that opens a choice, a test that replays the order answers through `Battle.PickPerk`, checking the tick and the offer against the table. A test with a board of its own names its pick before it reaches a level; with none named, the helper fails. A source test keeps `Battle.Tick()` out of every other scene test file. The EditMode replay of the real run gets the same pick step.

**Tests that change** (any other failing test is reported before it is touched)
- Every test that reads the real waves, enemy assets or reference order: `RealAssetRunTests`, `XpRealRunTests` (its level ticks; its comparison of the run with and without a table becomes 5b-AC4's battle with no pool), `ElementDataTests`, and the scene tests that replay the order (`RunSceneTests`, also its run on the real driver at a time scale of 50, which now answers each choice; `BonusSceneTests`, `MixedWaveSceneTests`, `DamageNumberSceneTests`, `ElementSceneTests`, `SelectionSceneTests`, `FloatingTextSceneTests`), where an amount of 20 or a tick is pinned after the first pick.
- `XpBarSceneTests`: a level reached by kills or by `AddXp` now opens a choice on the next tick.
- `BonusSceneTests` and `RunSceneTests`: the survived line was waited out in real seconds; it now ends by ticks.
- `ElementSceneTests`: the disc's range read from `RangeCells`.

**EditMode**
- **5b-AC1:** the modifier pin, the rate by its bits; additions before multiplications in both pick orders; an element perk leaves the other element and a tower of no element alone; each refusal.
- **5b-AC2:** a tower bought after picks; every tower on the board on a pick; a shot in flight keeps its damage and a running cooldown its time; the next launch adds the new cooldown; a target inside the new range and outside the old is shot on the next tick; the sell value and the gold check; a kill with two gold perks, an exit and a wave bonus with them.
- **5b-AC3:** the generator pin; 10 000 draws over pools of 3 to 6 hold three distinct places inside the pool; two progressions on one seed agree and two seeds differ; the first offer is the pin's first draw however much XP was added before it opens; pools of 0, 1 and 2.
- **5b-AC4 and AC5:** the pause pin, tick by tick; on a kill that both ends a wave and gains a level the gold holds the bonus when the choice opens; a level on the winning tick and one on the losing tick open none; two and three levels from one `AddXp` open choices for their levels in order, each with its own draw, `ChoiceNumber` and `ChoicesInARow`; `PickPerk` with no choice and with slots −1 and 3; a pick of a Shadow perk on a board of Light towers, then every tick against the run with no pool.
- **5b-AC8 and AC9:** every perk asset and the run asset against the document; each refusal with its message; the document's reference order with its picks, each choice on its tick with its offer, and its outcome; building nothing.
- **5b-AC16:** the determinism test of 4e-AC7 with the offers, picks and tower stats in its snapshot; `TickAllocation` over the real run with picks, the choice ticks included; source tests: Core has no `UnityEngine`; no production file writes `timeScale`; outside Core and the tests only `LevelUpView` calls `PickPerk(`, and nothing calls `Modifiers.Add(`.

**PlayMode**, in `Prototype.unity`
- **5b-AC6:** with the real driver and a time scale of 1, a choice open for a second of real time: the tick count and the fraction do not move; after the pick the next frame runs at most the ticks of its own time. `Hover`, `Click` on a free cell and on a tower, keys 1, 2 and Escape during a choice change nothing; the selection made before is still there after the pick.
- **5b-AC7:** two scene loads on one set seed show the same first offer; the static is null after the load; a load with none set starts a run.
- **5b-AC10:** hidden before the first level; on the frame after the choice tick: displayed, the title, the hint, and per card the texts, the tile's class, colour and letter against the perk asset and the palette; the resolved sizes and colours of 1.7; the overlay's order in the document; a second pick of one perk shows the stack line and a first shows none; two levels fed at once show `(1 of 2)` then `(2 of 2)` with their levels; `AssertLayout` unchanged while it shows.
- **5b-AC11:** `ClickButton` on a card during the lock does nothing and the cards ignore picking; after it, the cards take picking and a click sets the classes while `AwaitingChoice` stays true; a second click during the hold does nothing; after the hold the modifier is in the battle and the overlay is hidden, or the next choice is dealt with a new lock; the enter class holds one frame; the resolved transitions of the base rules and of each class; the panel's pick at the centres of the buy buttons, Sell and the close button answers the overlay (a real click is the browser's check, as for Sell).
- **5b-AC12 and AC13:** the panel after picks of each tower perk: value and tail texts, the tail hidden at no difference; open during a pick, rewritten on the frame after it, and no string built on the next frame; the disc of the selected tower, of a hovered one and of the tower to buy after a range pick; Sell's tail; a number reading 25 after a Light damage pick.
- **5b-AC14 and AC15:** the level line on a won and on a lost run; the survived line shown under an open choice for longer than its duration in real time, still shown after the pick, hidden once the battle has ticked the duration.
- **5b-AC16:** `Allocations.Of` each changed view's frame method on frames with nothing new: 0, the overlay open and closed.
- **5b-AC17:** the scene tests of 2b to 5a pass with the changes listed above only.

**Mutations the tests must kill**
- Modifiers: a multiplication before an addition; percents added up (15 for 15.625); the element ignored, or none read as "no tower"; a new tower left at its base; the towers on the board not refreshed; a shot in flight or a cooldown changed by a pick; the refund from an effective cost; the gold perk paid on an exit, on a bonus or twice.
- Draw: a constant or a shift off by one; the place taken by a remainder instead of the 64-bit product; the order not set afresh; a repeated perk; a number taken at construction; the second choice in a row showing the first offer.
- Pause and pick: the choice opened inside the projectile step (gold 10, not 17, at the pause); a choice on the tick that ends the run; a tick counted or a countdown moving while open; the slot applied as a perk's place; a pick accepted with no choice open.
- Gameplay: the driver gathering time during a choice (a burst after the pick); the seed never cleared; keys alive during a choice.
- Views: a card that picks during the lock; the pick sent on the click, not after the hold; the overlay left open, or closed between two choices in a row; the stack total before the pick; `+1.25 %`; panel values from the base; a tail of `+0.22`; the panel not rewritten on a pick; the hover's disc from the definition; the survived line on real time.

**Orchestrator in the browser, then Eduardo**
- Third kill of wave 1: everything on the board stands still, the screen darkens, `Level 2` and `Choose one`, three cards deal in one after another. Is there a hitch the first time? Is the board readable behind?
- Clicks during the first half second do nothing and no card lights up; then a card lights under the pointer and a click picks it: it holds, the others fade, the screen goes and the battle goes on with no jump and no burst of speed.
- During a choice: clicks on the board, the buy buttons and Sell do nothing; keys 1 and 2 do nothing; the countdown and the damage numbers stand still.
- `Wave 1 survived +40` is still on the line after the pick.
- After `Light towers +25 % damage`: Light towers' numbers read 25, a selected Light tower `12.5` with `+2.5`, a Shadow tower `10` with nothing. After range: the disc is wider, also on the hover before buying. After fire rate: `1.73` with `+0.23`. After gold: a kill adds 12.
- The panel open during a pick rewrites as the screen goes. The same perk a second time shows `Total +56 %`.
- A restart offers other cards. The end screen shows the level. Seven choices in a full run: too many, too few?
- Chrome and Safari, on the canvas and in full screen: the overlay covers everything, the cards are centred and sharp. The console shows no error.

## Risks
- **Two implementations.** The generator, the order of rule 17 and the pause are written twice; the three pins tie both to this plan, and the port reproduces them before it tunes.
- **A silent stall.** An unanswered choice stops a battle without an error. In the scene tests the `Tick()` helper and its source test catch it; a simulation must check `AwaitingChoice` itself.
- **`Tower.Stats` is now mutable.** Only the battle writes it. A view that kept a stat would go stale without a sign: the panel polls `Modifiers.Count`, the disc writes on a changed range, and `Board.TowerPlaced` is raised before the modifiers are applied, so its handlers read the cell and the element only. A tower placed through `Board.TryPlaceTower` has base stats until the next pick.
- **WebGL, the overlay's first show.** It lays out about twenty elements and draws text at two new sizes, and `%` may be a new glyph, all on the frame the battle stops, when nothing moves; the dealing starts a frame later. If the browser shows a hitch, the overlay is laid out at start and hidden by visibility. One more full-screen blended layer is drawn only while the battle stands still.
- **GC.** A choice builds the title, the hint and up to three stack lines; a pick grows the modifier list and rewrites an open panel. All of it happens on frames where the battle is stopped. The tick allocates nothing.
- **The pop class in one frame** is the technique of 5a's row, with its risk: here every start look is a class and nothing is written inline, so the engine never starts an ease from a stale style.
- **Float32.** Stacked multiplications are rounded at each step; the same operations run under Mono and IL2CPP (ADR 0003). The x1.25 stacks are exact; the x1.15 ones are pinned by their bits.
- **The cast to `decimal`.** `StatText` relies on it keeping seven significant figures, which .NET documents; the `1.73` test pins it under Mono, the browser check under IL2CPP.
- **A click in a batch run** cannot reach a button, so the lock is tested through the handler and the picking mode; the real click, the hover look and the overlay swallowing clicks are the browser's.
- **Clock seeds.** A player's run cannot be replayed from a report: the seed is not shown. Parked with the feedback pipeline.
- **`Progression` grows** to XP, the level, the pool, the offers and the counts, about 130 lines. The draw moves out when it gets weights.
- **Stacked on unaccepted steps.** 4b to 5a are tested and not accepted; a change asked there lands under this branch.
