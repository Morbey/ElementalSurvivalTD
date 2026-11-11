# Technical plan 0005 – Prototype slice 3, step 3b

Towers cost gold, kills pay gold, the screen shows the balance, and a click on a tower sells it. Architecture: `docs/adr/0004-run-waves-outcome-restart.md`, amended for gold. Numbers: `docs/balance/0001-prototype-economy.md` ("the balance document"). Prototype mode: this plan carries the acceptance criteria.

## Approach
Gold is one more piece of run state, so it lives in `Battle`, next to the lives: a whole number that starts from the run's data, drops on a purchase and rises on a kill or a sale. Towers are now bought and sold through `Battle`, which checks the gold and then asks `Board`. `Board` keeps its placement rule and knows nothing about gold, so every existing board, combat and wave test still places towers for free and stays as it is. A tower's cost travels in `TowerStats` and an enemy's reward in `EnemyStats`; the tower keeps the stats it was bought with, which is the cost it refunds. The starting gold and the refund are numbers of the run, in `RunDefinition`.

The refund is stored as a whole percent and computed with integers (`cost * percent / 100`, rounded down), so it is the same in the editor and in the browser. Gold is checked before the board rule: the check is free, and checking it last would need a dry run of the placement, flow field included. The HUD polls `Battle.Gold` like the lives. Waves with different enemy definitions already work (3a-AC1); every enemy still shares the one view prefab.

Discarded: gold on `Board` (space and economy in one class, and every board test would need gold); a wallet class (three lines of integer arithmetic); the refund as a float fraction (the last bit differs between Mono and IL2CPP); a refund percent per tower (the same number in every tower asset); required constructor parameters (about sixty edits in tests that have nothing to do with gold; the new ones default to no cost, no reward, no gold and a full refund); a Core guard on buying after the end (the input is already off, 3a-AC5); gold events, a shown price or sell value, and the modifier system (ADR 0001 keeps it deferred: nothing modifies gold yet).

## Acceptance criteria
- **3b-AC1 Starting gold.** A run starts with the starting gold of its `RunDefinition`. Gold is a whole number and never goes below 0.
- **3b-AC2 Buying.** A click on a cell with no tower tries to buy the tower of the placement input. With less gold than its cost the purchase is refused as `NotEnoughGold`, before any board check. Otherwise the board rule decides (2b-AC1 to AC5, 3a-AC11), and only a placed tower takes its cost from the gold. A refused purchase changes nothing.
- **3b-AC3 Message.** A purchase refused for gold shows "Not enough gold." in the placement message, like the four other refusals (2b-AC4).
- **3b-AC4 Kills pay.** On the tick a shot takes an enemy to health 0 or below, the gold rises by the gold reward of that enemy's definition, once per enemy, also when waves use different definitions and when the tower that fired has been sold. An enemy that reaches the exit pays nothing.
- **3b-AC5 Selling.** A click on a tower sells it: it leaves the board (2b-AC2) and the gold rises by its refund. The refund is the cost that tower was bought for while it has never launched a shot; from its first launch on it is that cost times the run's refund percent, divided by 100 and rounded down. A sale is never refused. This replaces the free towers and the free removal of 3a-AC11.
- **3b-AC6 After the end.** Once the run is won or lost, a click neither buys nor sells and the gold no longer changes (3a-AC5).
- **3b-AC7 HUD.** The screen shows "Gold N" with the wave and the lives, right from the frame after the gold changes. A restart shows the starting gold again.
- **3b-AC8 Data.** The starting gold and the refund percent come only from `Run_Prototype.asset`, the cost from `Tower_Prototype.asset` and each reward from its enemy asset. These values, the lives and the wave table (enemy definition, count, interval and delay per wave) equal the balance document, which replaces the values of 3a-AC12; every delay stays within 5 to 20 s. No code holds one of these numbers.
- **3b-AC9 Design target.** On the prototype map with those values, every run the balance document simulates ends as it says: at least building nothing (defeat), and a reference build order that wins, in which every purchase is affordable on its tick. At the start, the gold buys exactly the number of towers the document states and refuses the next. This replaces 3a-AC13; 2c-AC9 holds while the document leaves the combat stats of `Tower_Prototype` and the speed and health of `Enemy_Prototype` alone.
- **3b-AC10 Core and determinism.** Gold, buying and selling are rules of `ElementalTD.Core`, which still compiles with `noEngineReferences: true`. Outside Core and the tests nothing calls `Board.TryPlaceTower` or `Board.TryRemoveTower`. The same purchases and sales on the same ticks give the same gold, lives and outcome after every tick. The rest of 3a-AC14 holds.
- **3b-AC11 Allocation.** 3a-AC15 holds with kills that pay. A sale and a refused purchase allocate 0 bytes; a purchase allocates only its tower object. The gold text is built only when the number changes.
- **3b-AC12 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari. Everything of 2b, 2c and 3a that this plan does not replace still holds, the hover and the range disc included.

## Editor sequencing
As in plan 0004 and `docs/unity-cli-notes.md`. Before any batch or interactive editor, take the orchestrator's lock (`mkdir <scratchpad>/unity-batch.lock`) and release it after (`rmdir`).

| Order | Work | Editor on the worktree |
|---|---|---|
| 1 | T3b-1, T3b-2 and T3b-3 in parallel (separate files, signatures below), or one agent in that order: the code is under a hundred lines | Closed. One batch EditMode run as the compile check. |
| 2 | T3b-4, once the balance document exists | Open, then quit with a save. |
| 3 | Stage 4 review, then Stage 5 tests | Closed. |
| 4 | T3b-5 and the browser check | Closed. |

## Tasks

### T3b-1 – Core: gold
Serves 3b-AC1, AC2, AC4, AC5, AC10 and AC11. Code only.

```csharp
// PlacementResult: add NotEnoughGold, right after Placed: the battle checks it before the board rule.
// TowerStats: public readonly int Cost;        constructor (…, int cost = 0), rejects a cost below 0
// EnemyStats: public readonly int GoldReward;  constructor (…, int goldReward = 0), rejects a reward below 0
// Enemy:      public int GoldReward { get; private set; }   // set by Enter, like the speed and the health
// Tower:      internal bool HasFired;                       // set when it launches a projectile
// Board:      public Tower TowerOn(GridPos cell);           // null off the grid or with no tower; HasTower uses it

public sealed class Battle
{
    // Rejects a starting gold below 0 and a percent outside 0 to 100.
    public Battle(Board board, Wave[] waves, int lives, int startingGold = 0, int sellRefundPercent = 100);
    public int Gold { get; private set; }
    public PlacementResult TryBuyTower(GridPos cell, in TowerStats stats);
    public bool TrySellTower(GridPos cell);                  // false when the cell has no tower
}
```

- `TryBuyTower`: `NotEnoughGold` when `Gold < stats.Cost`; otherwise `Board.TryPlaceTower`, and on `Placed` the cost leaves the gold.
- `TrySellTower`: finds the tower with `Board.TowerOn`, removes it with `Board.TryRemoveTower` and adds `tower.Stats.Cost`, or `tower.Stats.Cost * percent / 100` in integers once `HasFired`.
- `StepTowers` sets `HasFired` on a launch. `StepProjectiles` adds `target.GoldReward` just before it removes a killed enemy. The tick order does not change.

**Done when:** Core has no `UnityEngine`, no `float` touches the gold, and the whole existing EditMode suite passes untouched.

### T3b-2 – Data and Gameplay: the numbers and the click
Serves 3b-AC1, AC2, AC5, AC6, AC8 and AC10. Code only.

- `TowerDefinition.cs`: `cost` (whole, at least 0), passed by `ToStats()`. `EnemyDefinition.cs`: `goldReward` (whole, at least 0), passed by `ToStats()`.
- `RunDefinition.cs`: `startingGold` (at least 0) and `sellRefundPercent` (0 to 100, default 100), exposed as `StartingGold` and `SellRefundPercent`.
- `PrototypeBootstrap.cs`: passes both to the `Battle` constructor.
- `TowerPlacementInput.cs`: `Click` calls `battle.TrySellTower(cell)` and, when that returns false, `battle.TryBuyTower(cell, tower.ToStats())`, raising `PlacementRefused` as today. `TryPick` picks nothing while there is no battle.

**Done when:** the files hold no number other than neutral field defaults, and Gameplay holds no rule beyond "sell, or else buy".

### T3b-3 – Presentation: the gold label and the message
Serves 3b-AC3, AC7 and AC11. Code and UI text files.

- `UI/Hud.uxml`: a label `gold-label` in `run-status`, after `lives-label`, ignored by picking. No new style.
- `RunHudView.cs`: a serialized `goldText` ("Gold {0}" in the scene); polls `Battle.Gold` and rewrites the label only when the number changes, like the lives; the label joins the start-up check.
- `PlacementMessageView.cs`: a serialized `notEnoughGoldText` and its case in `TextFor`.

**Done when:** the views hold no rule and no number.

### T3b-4 – Assets and scene wiring
Serves 3b-AC3, AC7 and AC8. Editor open on the worktree; after T3b-1 to T3b-3, and only once the balance document exists.

Through `unity command`, with the values of the balance document:
- `Tower_Prototype.asset`: the cost. `Enemy_Prototype.asset`: the reward. One new `Data/Assets/Enemy_<Name>.asset` per extra enemy definition the document lists.
- `Run_Prototype.asset`: the starting gold, the refund percent and the wave table.
- `Prototype.unity`, on `Hud`: `goldText` "Gold {0}" and `notEnoughGoldText` "Not enough gold.". Save the scene.

**Done when:** clean compile with an empty console; in Play the HUD shows the starting gold, a purchase lowers it, a kill raises it, "Not enough gold." shows once the gold runs out, and a sale refunds; everything is saved and `.vscode/settings.json` is reverted.

### T3b-5 – WebGL build
Serves 3b-AC12. Editor closed; after the tests pass. `WEBGL_PORT=8766 tools/webgl.sh` in the worktree. **Done when:** it exits with 0 and `http://localhost:8766` serves the new build.

## Tests for the qa-tester
On the worktree with its editor closed. The EditMode gold tests need no asset and can be written as soon as T3b-1 is done.

**3a tests that change**
- `RealAssetRunTests`: the 3a-AC12 test becomes the 3b-AC8 test, with the document's values. The three 3a-AC13 tests become the 3b-AC9 tests; the eight free towers go, replaced by the document's reference build order bought through `Battle.TryBuyTower` on its ticks.
- `SceneTestBase.PlaceEightTowers` goes; the two `RunSceneTests` that need a win play the reference build order through clicks on its ticks. The HUD texts they expect ("Wave 0/10", "Lives 10", "Next wave in 15 s", "8 s") follow the document if it changes the lives or the delays.
- The 2b scene tests click at most two purchases and need gold for both, because gold is checked first; they pass while the starting gold buys two towers. "Second click removes it" now also expects the gold back.
- Nothing else changes: the other EditMode tests place through `Board`, which is still free.

**EditMode**, on `Battle`, with stats chosen per test:
- **3b-AC1:** the battle starts with its starting gold; a negative starting gold, cost or reward and a percent outside 0 to 100 throw.
- **3b-AC2:** with gold equal to the cost the tower is placed and the gold is 0; with one less the result is `NotEnoughGold` and the tower count, the distances and the events are untouched. `NotEnoughGold` beats every board refusal; with enough gold each board refusal returns its own reason and costs nothing.
- **3b-AC4:** a kill pays on the tick the health reaches 0; two waves with different rewards pay their own, also when the object is reused; an exit pays nothing; two shots landing on one enemy in one tick pay once; a shot of a sold tower pays.
- **3b-AC5:** a tower that never launched refunds its cost, also after minutes in range of nothing; one tick after its first launch, with the shot still in flight, it refunds the percent, rounded down (cost 15 at 70 gives 10, cost 5 at 50 gives 2, 0 gives 0, 100 gives the cost). Two towers bought at different costs each refund their own. A sale on a free cell, a wall or off the grid returns false and changes nothing; a sale frees the cell and raises `TowerRemoved` once.
- **3b-AC8:** the assets hold the document's values, and `ToStats()`, `ToWaves()`, `StartingGold` and `SellRefundPercent` return them.
- **3b-AC9, named as a design target:** from the real assets, each run of the document; the first purchase past the stated number of starting towers returns `NotEnoughGold`.
- **3b-AC10:** two battles with the same purchases and sales on the same ticks have the same gold, lives and outcome after every tick of a whole run. A source test: no file outside `Core/` and `Tests/` contains `TryPlaceTower` or `TryRemoveTower`.
- **3b-AC11:** `Is.Not.AllocatingGCMemory` over several hundred ticks with kills that pay, after a warm-up, and over a sale and a refused purchase.

**PlayMode**, in `Prototype.unity`:
- **3b-AC7:** at start the label reads "Gold N" for the asset's starting gold; a click on a free cell lowers it by the cost on the next frame and a second click on that cell restores it; after a restart it reads the starting gold.
- **3b-AC3:** click free cells until the gold is below the cost: the next click shows "Not enough gold.", places no tower object and leaves the label alone.
- **3b-AC4, AC5:** with the reference build order, the label rises by the reward on the first kill; selling a tower that has fired raises it by the rounded-down refund.
- **3b-AC6:** after a defeat and after a win, a click on a tower and a click on a free cell change neither the tower count nor the label.
- **3b-AC12:** the 2b, 2c and 3a scene tests pass with the changes listed above.

**Orchestrator in the browser, then Eduardo**, at `http://localhost:8766` after a reload without cache, in Chrome and Safari:
- "Gold N" is easy to read and moves on every purchase, kill and sale.
- The starting gold does not buy everything: the player has to choose where the first towers go, and kills pay for the next ones.
- "Not enough gold." appears when it should and does not hide a useful message.
- A tower sold before it fires gives everything back; one sold after gives less. How does a misclick on a tower feel, with no confirmation?
- Building nothing ends in "Defeat"; a sensible build ends in "Victory"; a restart gives the starting gold back.
- The hover, the range disc, the waves, the lives and the end screen work as in 3a, and the browser console shows no error.

## Risks
- **The balance document is simulated outside Core**, in parallel with this plan. It must use the rules above: gold checked before the board, a kill paid on the tick the shot lands, "fired" meaning a launch, and the refund as a whole percent rounded down. If a 3b-AC9 check fails by a small margin, the criterion is corrected, not the code.
- **A misclick sells at a loss.** There is no confirmation and the price and the refund are shown nowhere: the player learns the cost from the first purchase. Both are out of scope; the browser check says whether they hurt.
- **Scene tests on real numbers.** The win tests replay the document's build order and the 2b tests need gold for two towers, so a rebalance can break them without a code change.
- **Optional constructor parameters.** A production call that forgets one gives free towers or no gold with no error. Each has one production call site, covered by the 3b-AC8 and 3b-AC7 tests.
- **HUD.** A fourth line covers more of the top-left cells (Parking lot). The label builds a string per change of gold, a few per second at most.
- **WebGL performance and GC.** The tick gains integer additions only. A purchase allocates its tower object, as in 2c.
- **Coupling.** The input now talks to `Battle` for both actions. `Board.TryPlaceTower` stays public for tests and simulations; the source test of 3b-AC10 keeps production code off it.
