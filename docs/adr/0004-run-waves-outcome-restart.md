# ADR 0004 – Runs: waves as data, the outcome and the restart

Status: Accepted (prototype slice 3a; gold added in step 3b; wave groups and the survival bonus in slice 4; the sell value as a query in step 4c). Amends ADR 0001 (enemy pooling, GPU instancing), ADR 0002 (clicks on the UI) and ADR 0003 (tick order, enemies). Amended by ADR 0005 (elements) and ADR 0006 (moments a view needs).

## Context
Slice 3a turns the one looping enemy of 2c into a run: ten waves of several enemies, lives lost at the exit, a win or a loss, and a restart. That needs a data format for waves, a home for the state of a run, many enemies drawn at once, and the first clickable UI. Step 3b adds gold: towers cost it, kills pay it and a sale refunds it. Slice 4 adds a gold bonus for each wave survived and the first buttons shown during play, and the design now lets a wave mix enemies of several elements.

## Decision
1. **A run is one asset.** `RunDefinition` holds the lives, the starting gold, the sell refund percent and the ordered waves. A wave is the unit of the wave count, the countdown and the survival bonus: it has the delay before it, its bonus and its groups in spawn order, and a group has an enemy definition, a count and the interval between spawns. They convert to Core `Wave` and `WaveGroup` values, so Core never sees a ScriptableObject. Until mixed waves are built every wave has one group, and `WaveSchedule` refuses any other number.
2. **Wave timing is its own Core class.** `WaveSchedule` counts the delays and intervals and says when an enemy enters, at most one per tick. `Battle` owns one and asks it once per tick.
3. **The state of a run lives in `Battle`**: the lives, the gold and a `RunOutcome` (running, won or lost). Gold is a whole number. Towers are bought and sold through `Battle`, which checks the gold and then asks `Board`; the placement rule of `Board` knows nothing about gold. A tower's cost and an enemy's reward travel in their stats, and a refund is integer arithmetic on a whole percent, so it is the same on every platform. What a sale pays is written once: `Battle` answers it as a query that changes nothing and pays a sale through that same query, so a view shows the amount and never computes it. Which tower the player has selected is input state in Gameplay and never enters `Battle`. An enemy remembers the wave it entered with; a wave is survived on the tick its last enemy has entered and none of its enemies is on the board, and it then pays its bonus if a life is left. A tick runs waves, enemies, towers, projectiles, wave bonuses and then the outcome, which is judged once, at the end of the tick, the loss first. A finished battle ignores `Tick()`, and the driver stops accumulating time.
4. **Enemies are pooled like projectiles.** `Board` owns a free list of enemy objects: `SpawnEnemy` takes one and gives it the wave's stats, `RemoveEnemy` returns it. The entry count of ADR 0003 still tells a reused object from the one a shot was fired at. In Presentation the n-th pooled enemy view shows the n-th enemy on the board; views stay anonymous.
5. **A restart reloads the scene.** The state of a run is never reset in place: a fresh run is a new `Board`, a new `Battle` and new views.
6. **The HUD polls.** Views read `Battle` every frame and rewrite a label only when its number changes; the line that announces a survived wave polls a count of survived waves, and the tower panel polls the sell value of the selected tower. The HUD and the end screen live in the one `UIDocument`.
7. **The board and the UI never share a click.** During a run no element that takes clicks lies over a cell: the HUD layout keeps the board in an area of its own (ADR 0002). The end screen covers the board only once the run is over, and board input is then off.

## Alternatives
- **One asset per wave.** Ten files to keep in order, plus a list of them. A run is edited and balanced as a whole. Rejected.
- **A flat list of groups, with a mark on those that continue the previous wave.** No new type, but the wave count, the delay and the bonus would hang on whichever group opens a wave. Rejected.
- **Wave timing inside `Battle`.** One file less, but the timing could not be tested without a board, and `Battle` would mix combat, waves and soon gold. Rejected.
- **Gold on `Board`, or in a class of its own.** `Board` would mix space with economy, and its tests would need gold; a wallet class would wrap three lines of integer arithmetic. Revisit a class when interest and its cap arrive. Rejected.
- **The refund as a float fraction.** Rounding a float product can differ in the last bit between Mono and IL2CPP, which is a whole gold piece at the margin. Rejected.
- **A counter of enemies on the board per wave.** Constant time, but it must be kept in step at every place an enemy leaves. One pass over a few dozen enemies per tick needs no bookkeeping. Rejected.
- **A Core event for a survived wave.** The tick would call into Presentation and build a text inside `Tick()`; a polled count does neither. Rejected, as in ADR 0003.
- **Create every enemy object of the run up front.** No reuse and no entry count, but it breaks as soon as the number of enemies is not fixed at the start (the enemies-per-wave modifier, enemies that split). Rejected.
- **A view bound to each enemy through spawn and death events.** Needed once a view has state of its own, such as a death effect. Not yet; see ADR 0003.
- **Reset in place** (clear the board, reset the battle, rebind the views). A faster restart, but every new piece of state needs its own reset, and a missed one leaks into the next run. Rejected.
- **`Time.timeScale = 0` at the end.** It would have to be undone on restart, and a simulation that calls `Tick()` directly would not stop. Rejected.
- **Ask the UI panel what is under the pointer before every board click.** The general rule; deferred until an element that takes clicks lies over a board that takes input.
- **GPU instancing for enemies.** 3a has about twenty enemies on the board at most, three renderers each, on the SRP Batcher. Still deferred, until a browser measurement says otherwise.

## Consequences
- A balance simulation can play a whole run and read its outcome with Core alone.
- A restart costs a scene load: a short hitch after a click, and one garbage collection.
- `Tick()` creates an enemy object only when more enemies are on the board than ever before in the run.
- An enemy view may show a different enemy from one frame to the next, so a view keeps nothing that belongs to one enemy.
- Every enemy shares one view prefab, and its element changes only its material (ADR 0005). A look per enemy definition needs a pool per prefab.
- A mixed wave is a wave with more than one group. That step adds the walk over the groups in `WaveSchedule`, with the gap between two groups, and lifts the refusal; the data format, the wave count, the countdown, the bonus and the HUD already take it.
- A screen that covers the board during a run (the perk choice) must turn board input off, as the end screen does, or bring the general click rule.
- The HUD polls the gold, so nothing announces a single kill or sale. A view that needs the moment itself (a gold pop-up) reads a log like the hit log of ADR 0006.
- `Board.TryPlaceTower` and `TryRemoveTower` stay public for tests and simulations; production code outside Core buys and sells through `Battle` only.
