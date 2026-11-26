# ADR 0007 – Modifiers, the perk choice and the pause

Status: Accepted (prototype slice 5, step 5b). Amends ADR 0001 (the modifier system and the seeded generator apply), ADR 0003 (the pause), ADR 0004 (what stops a battle, what `Progression` holds, a screen over the board during a run) and ADR 0005 (the modifier system arrives).

## Context
Step 5b turns a level into a choice: the battle stops, three perks are offered, and the one picked changes the towers. It is the first user of three rules of CLAUDE.md section 7 that were deferred until now (ADR 0001): one modifier system, a seeded and injectable generator, and content as data for perks. It is also the first stop of the battle that is not its end, and the first screen that covers the board during a run. A tower's stats were fixed at its purchase and read straight from data; the balance port must reproduce every draw and every changed stat.

## Decision
1. **One list of modifiers per battle.** `Modifiers` is a Core class that `Battle` owns and makes itself. A modifier is a value: the stat it changes, an element or none, the operation (add or multiply), the amount and its source. The stat is a Core enum, `Stat` (not `Attribute`, which is `System.Attribute`), and names its owner: a tower stat or a global one. The element narrows a tower stat to the towers of that element; none means every tower. Stat and element together are the target of section 7. The source says who added the modifier, the perk's place in the pool today. Nothing removes a modifier yet.
2. **The order is fixed.** The effective value is the base, then every addition on that stat in the order added, then every multiplication in the order added, rounded to float32 after each. A percent perk is a multiplication, so two of them compound. Clamps are the third stage and hold no bound yet: no modifier of this step lowers a stat, and the perk data refuses one that would.
3. **A whole stat takes whole additions only.** Gold is a whole number (ADR 0004), and a multiplied amount could round differently under Mono and IL2CPP. A modifier that multiplies a whole stat, or adds a fraction to it, is refused when it is made.
4. **A tower caches its effective stats.** It keeps the stats it was bought with and its effective ones; the battle recomputes them when the tower is bought and for every tower on a pick. The tick reads a struct, as before. What a perk must not move reads the base: the cost and the sell value. `Modifiers` also answers the effective stats of a tower not yet bought, and changes nothing.
5. **A perk is data.** `PerkDefinition` holds one modifier and the words of its card. The run asset lists its pool in order, and a perk's place in that list is its identity: the source of its modifier, what the draw returns and what the port names.
6. **`Progression` holds the pool and the offers.** It receives the pool as modifiers, counts the levels gained and not yet answered, draws each offer and counts how often each perk was taken. `Battle` applies a pick. A progression with no pool never opens a choice.
7. **Randomness is a generator of our own.** `Rng` is a Core class on 32-bit whole numbers only, stated exactly in plan 0012 with its pins, so the port copies it in a few lines. Whatever draws owns its generator and takes its seed; nothing shares a stream. An offer is a partial shuffle of the pool's places in an array made once.
8. **The pause is battle state.** A battle awaiting a choice ignores `Tick()` and counts nothing, like a finished one, and the driver gathers no time. The choice opens at the end of a tick that leaves the run running, after the wave bonuses and the outcome. `Time.timeScale` is not touched.
9. **The choice screen follows Core.** The view polls a count of the choices opened and whether one is open. The lock and the hold of the visual direction are the view's, in real time; a pick reaches Core when the hold ends, so the battle stays stopped under the picked look. The view calls `Battle.PickPerk` itself: no input state lies between a card and the battle.
10. **Board input is off while a choice is open.** The placement input checks it where it checks the outcome, and the overlay takes every click on the UI (ADR 0004, decision 7).
11. **A scene run takes its seed from a static on the bootstrap,** read and cleared when the battle is built, because nothing can reach the battle before `Awake`. With none set, the seed comes from the clock.

## Alternatives
- **`Time.timeScale = 0` for the pause.** A simulation that calls `Tick()` would not stop, the views' real-time looks would need unscaled time anyway, and it must be undone on every way out. Rejected, as in ADR 0004.
- **Effective stats computed when read.** No cache to refresh, but a pass over the modifiers per tower and per tick for values that change seven times a run. Rejected.
- **A list of modifiers on each tower.** A new tower would copy the list, and a perk for every tower would exist once per tower. Rejected.
- **A third operation, percent that adds up** (two picks of +25 % make +50 %). Section 7 states two operations. If play shows compounding runs away, it is one more stage between the two.
- **Stats as assets with ids, like elements.** Every stat is read by a line of code that must know it, so an asset would only hide the enum. Rejected.
- **A strategy class per perk.** New code for every perk whose effect is a number. Rejected; a perk with new behaviour brings its own code when it comes.
- **`System.Random`.** Its sequence belongs to the runtime, so the port could not copy it. Rejected.
- **A linear congruential generator.** The first numbers of neighbouring seeds are close, so a crowd of forty seeded runs would see nearly the same first offer. **Xorshift** cannot take the seed 0. Rejected for a counter stepped by a constant and mixed (SplitMix).
- **The seed, or the pool, as another optional parameter of `Battle`.** Its constructor already takes seven; `Progression` is where the draw lives. Rejected.
- **The offers in a class of their own.** `Progression` stays small, and the draw needs the levels it counts. Split it when the draw gets weights or luck.
- **A Core event for an opened choice.** Rejected for a polled count, as in ADR 0003.
- **The card's words derived from the modifier,** with one text per stat on the view. No word on the asset, but a perk's wording is content, and a perk of several modifiers will need its own. Rejected.
- **The pick through a Gameplay component.** A method that only passes the call on. Rejected.

## Consequences
- A new perk on a known stat is one asset. A new stat is a member of `Stat` and the line that reads it. `Stat` and `Operation` are serialized on assets by value: append, never reorder.
- `Tower.Stats` changes during a run. A view reads it when it draws and never keeps a stat; one that shows text polls `Modifiers.Count`.
- `Board.TowerPlaced` is raised before the battle applies the modifiers, so a handler reads only what no modifier changes: the cell and the element. A tower placed through `Board.TryPlaceTower` has its base stats until the next pick.
- A simulation or a test must answer every choice: an unanswered one stops the battle for good, and `Tick()` says nothing. Core does not refuse a purchase or a sale during a choice: the input refuses the board click and the keys, and the overlay takes the buttons.
- A run is reproducible from its seed and its picks. The game shows no seed, so a player's run cannot be replayed from a report yet.
- A second random rule (critical hits) takes its own generator and seed. A modifier on an enemy (wave health, Slowed) needs enemy stats in `Stat` and removal by source.
- The tower's damage enters the chart's lookup already modified (ADR 0005, decision 9).
- A screen that stops the battle for another reason (a menu, a manual pause) is one more condition where `AwaitingChoice` is read.
