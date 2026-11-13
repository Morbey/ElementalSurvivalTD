# ADR 0004 – Runs: waves as data, the outcome and the restart

Status: Accepted (prototype slice 3a; gold added in step 3b; wave groups and the survival bonus in slice 4; the sell value as a query in step 4c; the walk over the groups in step 4e; XP and the level in step 5a). Amends ADR 0001 (enemy pooling, GPU instancing), ADR 0002 (clicks on the UI) and ADR 0003 (tick order, enemies). Amended by ADR 0005 (elements) and ADR 0006 (moments a view needs, what a damage number keeps).

## Context
Slice 3a turns the one looping enemy of 2c into a run: ten waves of several enemies, lives lost at the exit, a win or a loss, and a restart. That needs a data format for waves, a home for the state of a run, many enemies drawn at once, and the first clickable UI. Step 3b adds gold: towers cost it, kills pay it and a sale refunds it. Slice 4 adds a gold bonus for each wave survived and the first buttons shown during play, and the design now lets a wave mix enemies of several elements. Step 5a counts XP from kills and a level, the ground for the perk choice.

## Decision
1. **A run is one asset.** `RunDefinition` holds the lives, the starting gold, the sell refund percent, the level table and the ordered waves. The level table is whole numbers written out, the n-th the XP from level n to n + 1. A wave is the unit of the wave count, the countdown and the survival bonus: it has the delay before it, its bonus and its groups in spawn order, and a group has an enemy definition, a count and a spawn interval. They convert to Core `Wave` and `WaveGroup` values, so Core never sees a ScriptableObject. The groups enter one after another: the first enemy of a wave enters when its delay ends, and every other enemy enters its own group's spawn interval after the enemy before it. A mixed wave is a wave with groups of several elements.
2. **Wave timing is its own Core class.** `WaveSchedule` counts the delays and intervals and says when an enemy enters, at most one per tick. `Battle` owns one and asks it once per tick.
3. **The state of a run lives in `Battle`**: the lives, the gold and a `RunOutcome` (running, won or lost). Gold is a whole number. Towers are bought and sold through `Battle`, which checks the gold and then asks `Board`; the placement rule of `Board` knows nothing about gold. A tower's cost and an enemy's gold and XP rewards travel in their stats, and a refund is integer arithmetic on a whole percent, so it is the same on every platform. What a sale pays is written once: `Battle` answers it as a query that changes nothing and pays a sale through that same query, so a view shows the amount and never computes it. Which tower the player has selected is input state in Gameplay and never enters `Battle`. An enemy remembers the wave it entered with; a wave is survived on the tick its last enemy has entered and none of its enemies is on the board, and it then pays its bonus if a life is left. A tick runs waves, enemies, towers, projectiles, wave bonuses and then the outcome, which is judged once, at the end of the tick, the loss first. A finished battle ignores `Tick()`, and the driver stops accumulating time.
4. **Enemies are pooled like projectiles.** `Board` owns a free list of enemy objects: `SpawnEnemy` takes one and gives it its group's stats, `RemoveEnemy` returns it. The entry number of ADR 0003 still tells a reused object from the one a shot was fired at. In Presentation the n-th pooled enemy view shows the n-th enemy on the board; views stay anonymous.
5. **A restart reloads the scene.** The state of a run is never reset in place: a fresh run is a new `Board`, a new `Battle` and new views.
6. **The HUD polls.** Views read `Battle` every frame and rewrite a label only when its number changes; the line that announces a survived wave polls a count of survived waves, and the tower panel polls the sell value of the selected tower. The HUD and the end screen live in the one `UIDocument`.
7. **The board and the UI never share a click.** During a run no element that takes clicks lies over a cell: the HUD layout keeps the board in an area of its own (ADR 0002). The end screen covers the board only once the run is over, and board input is then off.
8. **XP and the level are their own Core class.** `Progression` holds the run's XP, a whole running total, its level and the level table. `Battle` owns one, as it owns the schedule; given none, it makes one with no table, whose level never rises. A kill pays its XP where it pays its gold, and the level rises on that kill, before the tick's wave bonuses and its outcome.

## Alternatives
- **One asset per wave.** Ten files to keep in order, plus a list of them. A run is edited and balanced as a whole. Rejected.
- **A flat list of groups, with a mark on those that continue the previous wave.** No new type, but the wave count, the delay and the bonus would hang on whichever group opens a wave. Rejected.
- **A gap between two groups as a number of its own, on the wave or on the group.** One more field, dead on a wave of one group or at odds with the wave's delay on every first group, and its only new use is a pause, which pulls two elements out of one tower's range. Rejected; if content proves it needs a pause, it is one field on the group.
- **Groups that run at the same time, each with an offset from the wave's start.** An alternating stream would be two groups instead of many, but the schedule needs a timer per group and lets several enemies enter on one tick. Rejected.
- **Wave timing inside `Battle`.** One file less, but the timing could not be tested without a board, and `Battle` would mix combat, waves and soon gold. Rejected.
- **Gold on `Board`, or in a class of its own.** `Board` would mix space with economy, and its tests would need gold; a wallet class would wrap three lines of integer arithmetic. Revisit a class when interest and its cap arrive. Rejected.
- **The refund as a float fraction.** Rounding a float product can differ in the last bit between Mono and IL2CPP, which is a whole gold piece at the margin. Rejected.
- **XP and the level as fields of `Battle`,** like the gold. The table and the carry-over, and next the perk offers, would join combat, waves and gold in one class, and none could be tested without a board. Rejected.
- **The level curve as a formula** (a base and a growth). Two numbers instead of ten, but a power of floats can round differently under Mono and IL2CPP, a whole level at the margin, and balance tunes single steps. Rejected.
- **A counter of enemies on the board per wave.** Constant time, but it must be kept in step at every place an enemy leaves. One pass over a few dozen enemies per tick needs no bookkeeping. Rejected.
- **A Core event for a survived wave.** The tick would call into Presentation and build a text inside `Tick()`; a polled count does neither. Rejected, as in ADR 0003.
- **Create every enemy object of the run up front.** No reuse and no entry number, but it breaks as soon as the number of enemies is not fixed at the start (the enemies-per-wave modifier, enemies that split). Rejected.
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
- A block of one element and an alternating stream are both content: groups in order, down to groups of one enemy. A pause between two groups longer than the next group's interval cannot be written.
- A screen that covers the board during a run (the perk choice) must turn board input off, as the end screen does, or bring the general click rule.
- A level is gained inside the tick, before the wave bonuses. Whatever stops the battle for it (the perk choice) waits for the end of that tick, so the bonus of a kill that both ends a wave and gains a level is paid first.
- `Progression.AddXp` stays public for tests and simulations; outside Core only the battle's kill adds XP.
- The HUD polls the gold, so nothing announces a single kill or sale. A view that needs the moment itself (a gold pop-up) reads a log like the hit log of ADR 0006.
- `Board.TryPlaceTower` and `TryRemoveTower` stay public for tests and simulations; production code outside Core buys and sells through `Battle` only.
