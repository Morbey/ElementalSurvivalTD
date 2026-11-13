# 0001 – Prototype economy (step 4e, mixed waves)

Provisional until playtests. Simulated on a float32 port of Core (`tools/balance/core.py`) that follows the rules for the port of plans 0007 (1 to 10) and 0010 (11 to 15) and reproduces:
- the 3c pins the real Core confirmed (`verify3c.py`);
- step 4b on its own numbers (`verify4b.py`): building nothing loses on tick 2931 with 175 gold, and the 4b reference order wins on tick 10165 with 10 lives, 105 gold and 16/12 towers;
- both pins of plan 0010 (`verify4e.py`): the schedule pin's entry ticks 61, 121, 151, 181 and 301 with its counters, and the mixed-fire pin tick by tick (launches 1, 40, 80, 120; the hit on tick 131 at (1.97, 4.97) takes the Shadow enemy to 25; exits 652 and 712; won with 7 gold; 4 launches, 3 wasted, 1 recoverable).

The gold rules are those of plan 0005: gold is checked before the board, a kill pays on the tick its shot lands, "fired" means a launch (also at an immune enemy), and the refund is `cost * percent / 100` in integers. Every damaging hit is x2: a tower deals 20 to the other element and 0 to its own.

## Values

Changed from step 4b: waves 4 to 10 (groups and health) and the enemy assets. Nothing else.

| Value | Where | Number | Confidence |
|---|---|---|---|
| Chart | `ElementChart.asset` | Light against Shadow x2, Shadow against Light x2; own element 0 by rule | high (Eduardo's rule) |
| Towers | `Tower_Light`, `Tower_Shadow` | cost 40, range 2.5, damage 10, 1.5 shots/s, projectile 10; they differ only in element | medium |
| Wave bonus | every wave of `Run_Prototype` | 40, then 20 rising by 5 a wave to 60 (400 per run) | low |
| Starting gold | `Run_Prototype.startingGold` | 115 (buys exactly 2 towers, leaves 35; the 3rd is `NotEnoughGold`) | medium |
| Gold per kill | `goldReward` on every enemy asset | 10 | medium |
| Sell refund after the first launch | `Run_Prototype.sellRefundPercent` | 50 (20 gold) | low |
| Lives | `Run_Prototype.lives` | 10 | medium |
| Enemy speed | all enemies | 2 | – |
| Wave health, waves 4 to 10 | the enemy assets below | 100, 100, 180, 180, 260, 360, 420 | low (waves 9 and 10: medium-low, they decide the run) |

Groups are written in spawn order, `S` for a Shadow group and `L` for a Light group, with the count: `S3 L3` is 3 Shadow enemies, then 3 Light. **Every group's interval is 1 s**, and all groups of a wave share its health, so the enemy asset of a group is `Enemy_Shadow<health>` or `Enemy_Light<health>`.

| Wave | Delay s | Groups in order | Enemies | Health | Hits to kill | Bonus |
|---|---|---|---|---|---|---|
| 1 | 15 | `S3` | 3 | 45 | 3 | 40 |
| 2 | 8 | `L4` | 4 | 55 | 3 | 20 |
| 3 | 8 | `L5` | 5 | 70 | 4 | 25 |
| 4 | 8 | `S3 L3` | 6 | 100 | 5 | 30 |
| 5 | 8 | `L4 S3` | 7 | 100 | 5 | 35 |
| 6 | 8 | `S1 L1 S1 L1 S1 L1 S1 L1` | 8 (4 S, 4 L) | 180 | 9 | 40 |
| 7 | 8 | `S2 L1 S2 L1 S2 L1` | 9 (6 S, 3 L) | 180 | 9 | 45 |
| 8 | 8 | `L2 S1 L2 S1 L2 S1 L1` | 10 (7 L, 3 S) | 260 | 13 | 50 |
| 9 | 8 | `S1 L1` five times, then `S1` | 11 (6 S, 5 L) | 360 | 18 | 55 |
| 10 | 8 | `L1 S1` six times | 12 (6 L, 6 S) | 420 | 21 | 60 |

**Enemy assets** (13), every one speed 2 and reward 10: `Enemy_Shadow45`, `Enemy_Light55`, `Enemy_Light70`, `Enemy_Shadow100`, `Enemy_Light100`, `Enemy_Shadow180`, `Enemy_Light180`, `Enemy_Shadow260`, `Enemy_Light260`, `Enemy_Shadow360`, `Enemy_Light360`, `Enemy_Shadow420`, `Enemy_Light420`. From step 4b, `Enemy_Shadow45`, `Enemy_Light55`, `Enemy_Light70` and `Enemy_Shadow180` stay; `Enemy_Shadow85`, `Enemy_Light105`, `Enemy_Shadow135`, `Enemy_Light245`, `Enemy_Shadow345` and `Enemy_Shadow460` go; the other nine are new.

**Entry ticks are those of step 4b**: the first enemy of waves 1 to 10 enters on ticks 901, 1501, 2160, 2880, 3660, 4500, 5400, 6360, 7380 and 8460, and the 75th and last on tick 9120.

## How the waves were chosen

- **Teaching order.** Waves 1 to 3 teach one element at a time and stay as they were. Waves 4 and 5 are the first mixed waves, as two blocks, so the player sees one element and then the other. Wave 6 is the first stream, alternating one by one. Waves 7 and 8 are uneven streams (Shadow-heavy, then Light-heavy), so the announcement still says which element to add. Waves 9 and 10 are even streams and the hardest waves.
- **Health counts in hits.** Every damaging hit deals 20, so an enemy needs `ceil(health / 20)` hits: 345, 360 and 380 play the same. Every health from wave 4 on is a whole number of hits, and the sensitivities move it by whole hits.
- **The run turns on waves 9 and 10.** On the proposal, scaling one wave at a time (`explore4e.py margin`), 40 sloppy readers still win 38 to 40 runs with wave 4, 5 or 7 at +30% and leak at most 0.3 enemies a run there, while +15% on wave 9 alone already leaks 4.8 and costs 8 of 40 runs. Raising the mid waves enough to make them tight compounds: each leak also costs a kill's gold, and the run collapses (`[160, 160, 250, 250, 320, 370, 370]`: 5 of 40 sloppy readers win). So waves 4 to 8 test the **mix** (one-element boards die there), and waves 9 and 10 test the **amount**.
- **Health candidates** (`explore4e.py health`, wins in 40 sloppy readers / half and half, median lives): the 4b health on these groups wins 39 / 37 (median 6 / 5), but the saver and the late starter lose. `[120, 120, 200, 200, 280, 360, 440]` wins 38 / 38 (8 / 9), but the saver still loses and +1 hit drops it to 23 / 22. The proposal wins 40 / 40 (9 / 8), the saver wins with 6 lives, and +1 hit still wins 33 / 34 (7 / 6).
- **The pattern changes little in outcomes, a lot in what the player sees** (`explore4e.py patterns`, same health). Streams everywhere: 38 / 40 wins, 31% recoverable launches. Two blocks everywhere: 39 / 40, 6% recoverable. Pairs (`SSLL`): 40 / 40, 23%. A Shadow-only wave 7: 39 / 40, and the saver barely survives (1 life). The proposal sits between: 40 / 40, 28%.

## Strategies

Bots act every 0.5 s unless stated. Cells:
- **Sensible**: each tower on the free cell off the route that adds the most path in range.
- **Maze**: an expert order of 32 cells found offline by a two-step lookahead on path in range, with route changes allowed.
- **Sloppy human**: acts every 2 s and takes a seeded-random cell among the 6 best sensible ones.

Elements:
- **Reads**: reads the label and the board: per element, the enemy health on the board plus the announced wave's, per tower that damages it, and buys the counter of the largest.
- **Half and half**: ignores the label and buys whichever element it has fewer of.
- **Random**: a seeded coin per purchase.
- **60/40 or 70/30**: ignores the label and keeps that share of one element. The rounding makes it more uneven while the board is small: the 4th tower of 60/40 makes 3/1.
- **One element after wave 3**: reads during waves 1 to 3, then buys one element only, like a player who has not noticed the mixing.

End ticks count `Tick()` calls, including the one that ends the run. Gold includes the bonuses. **Wasted** is the share of launches at an immune enemy; **recoverable** is the share of launches at an immune enemy while one the tower could damage was in its range, which is what skipping the own element would save. **Both elements** is the share of ticks with enemies on the board that hold both elements.

| Strategy | Outcome | End | Lives left | Kills | Towers Light/Shadow at end | Gold at 30/60/90/120/150 s | Gold at end | Wasted / recoverable | Both elements |
|---|---|---|---|---|---|---|---|---|---|
| Builds nothing | Lost | tick 2931, 48.9 s | 0 | 0 | 0/0 | 155/–/–/–/– | 175 | – | 11% |
| Reads, sensible | Won | tick 9798, 163.3 s | 10 | 75 | 14/15 | 25/0/15/30/25 | 105 | 50% / 28% | 59% |
| Reads, maze | Won | tick 10317, 171.9 s | 10 | 75 | 14/15 | 25/30/15/10/25 | 105 | 52% / 29% | 66% |
| Half and half, Light first | Won | tick 9760, 162.7 s | 9 | 74 | 15/14 | 25/0/25/30/15 | 95 | 50% / 29% | 63% |
| Half and half, Shadow first | Won | tick 9912, 165.2 s | 7 | 72 | 14/15 | 25/0/25/30/25 | 75 | 51% / 28% | 61% |
| Half and half, maze (Light / Shadow first) | Won | 171.5 / 170.3 s | 10 | 75 | 15/14, 14/15 | – | 105 | 49%, 50% / 26%, 27% | 68%, 66% |
| Light only | Lost (wave 5) | tick 3746, 62.4 s | 0 | 6 | 6/0 | 25/20/–/–/– | 20 | 84% / 0% | 21% |
| Shadow only | Lost (wave 6) | tick 5239, 87.3 s | 0 | 19 | 0/11 | 35/10/–/–/– | 15 | 68% / 19% | 39% |
| Reads to wave 3, then Light only | Lost (wave 9; leaks 6 in wave 8) | tick 8592, 143.2 s | 0 | 52 | 20/3 | 25/30/15/0/– | 0 | 64% / 25% | 51% |
| Reads to wave 3, then Shadow only | Lost (wave 9; leaks 4 in wave 7) | tick 8394, 139.9 s | 0 | 50 | 2/20 | 25/10/15/30/– | 20 | 63% / 27% | 51% |
| Reads, sells 2 and rebuilds at wave 8 | Won | tick 9794, 163.2 s | 10 | 75 | 13/15 | 25/0/15/20/25 | 105 | 49% / 28% | 60% |
| Late: first tower at wave 2, reads | Lost (wave 9) | tick 8454, 140.9 s | 0 | 48 | 11/11 | 35/10/25/30/– | 0 | 54% / 34% | 58% |
| Saver: 2 at start, then batches of 3, reads | Won | tick 9808, 163.5 s | 6 | 71 | 15/11 | 105/0/95/100/25 | 185 | 49% / 28% | 65% |

| Crowd (40 seeds each) | Won | Median lives (range) | Median gold at end (wins) | Wasted / recoverable | Both elements |
|---|---|---|---|---|---|
| Sloppy human, reads | 40 | 9 (4–10) | 105 | 50% / 28% | 65% |
| Sloppy human, half and half | 40 | 8 (2–10) | 85 | 50% / 28% | 66% |
| Sloppy human, random elements | 34 | 8 (0–10) | 95 | 51% / 28% | 65% |
| Sensible, random elements | 35 | 9 (0–10) | 95 | 52% / 28% | 62% |
| Sloppy human, 60/40 | 30 | 5 (0–10) | 95 | 52% / 30% | 67% |
| Sloppy human, 70/30 | 2 | 0 (0–4) | 85 | 57% / 25% | 57% |
| Sloppy human, one element after wave 3 | 0 | 0 | – | 62% / 25% | 55% |
| Sloppy human, one element | 0 | 0 | – | 74% / 14% | 28% |

**Per wave**: recoverable / wasted launches (counted on the wave of the enemy shot at), the share of that wave's busy ticks with both elements on the board, and the mean leaks.

| Wave | Groups | Sloppy readers (40) | Sloppy half and half (40) | Reference order |
|---|---|---|---|---|
| 1 | `S3` | 0% / 16%, 0%, 0.0 | 0% / 52%, 14%, 0.5 | 0% / 0%, 0%, 0 |
| 2 | `L4` | 0% / 52%, 0%, 0.0 | 0% / 44%, 18%, 0.0 | 0% / 61%, 0%, 0 |
| 3 | `L5` | 0% / 41%, 1%, 0.0 | 0% / 44%, 1%, 0.0 | 0% / 50%, 9%, 0 |
| 4 | `S3 L3` | 6% / 47%, 31%, 0.0 | 9% / 50%, 41%, 0.0 | 0% / 45%, 52%, 0 |
| 5 | `L4 S3` | 6% / 50%, 29%, 0.0 | 5% / 50%, 29%, 0.0 | 11% / 50%, 53%, 0 |
| 6 | stream 4 S, 4 L | 29% / 50%, 90%, 0.0 | 30% / 51%, 91%, 0.0 | 28% / 50%, 92%, 0 |
| 7 | stream 6 S, 3 L | 27% / 51%, 88%, 0.0 | 26% / 51%, 87%, 0.0 | 29% / 48%, 89%, 0 |
| 8 | stream 7 L, 3 S | 28% / 50%, 90%, 0.0 | 28% / 50%, 89%, 0.0 | 28% / 58%, 92%, 0 |
| 9 | stream 6 S, 5 L | 33% / 50%, 99%, 0.4 | 33% / 50%, 99%, 0.7 | 36% / 51%, 100%, 0 |
| 10 | stream 6 L, 6 S | 35% / 50%, 97%, 0.9 | 34% / 50%, 96%, 1.0 | 35% / 52%, 97%, 0 |

## Reference build order (wins)

Each action runs after the given number of `Tick()` calls, before the next one, with the tower selected before the click. Every action is accepted, the four-cell occupancy rule included. It is the maze order with the reading bot's elements and no swap (`refrepair.py 0 0`, `ref4e.json`). The swaps tried (2 towers at wave 7, 8, 9 or 10) win with fewer lives or lose.

**Outcome:** Won on the 10317th `Tick()` (171.9 s), with 10 lives, 75 kills, 105 gold left and 29 towers (14 Light, 15 Shadow); the last wave's bonus is paid on that tick. The first kill pays 10 on tick 1288. 52% of its launches are wasted on immune enemies, 29% with a damageable enemy in range.

**Robustness:** with every action after tick 0 shifted 1 to 30 ticks later, no action is refused and the run still wins with 10 lives, 105 gold and 14/15 towers. It ends on tick 10267 or 10317.

| Tick | Cell | Tower | Action | Gold change | Gold after |
|---|---|---|---|---|---|
| 0 | (0,8) | `Tower_Light` | buy | −40 | 75 |
| 0 | (0,8) | `Tower_Light` | sell, never fired | +40 | 115 |
| 0 | (11,2) | `Tower_Light` | buy | −40 | 75 |
| 0 | (11,3) | `Tower_Light` | buy | −40 | 35 |
| 1290 | (14,2) | `Tower_Shadow` | buy | −40 | 5 |
| 1470 | (13,2) | `Tower_Shadow` | buy | −40 | 25 |
| 2130 | (12,0) | `Tower_Shadow` | buy | −40 | 5 |
| 2220 | (10,1) | `Tower_Shadow` | buy | −40 | 5 |
| 2910 | (4,7) | `Tower_Light` | buy | −40 | 5 |
| 3360 | (3,7) | `Tower_Light` | buy | −40 | 10 |
| 3630 | (4,6) | `Tower_Shadow` | buy | −40 | 0 |
| 3840 | (6,8) | `Tower_Shadow` | buy | −40 | 10 |
| 4110 | (1,6) | `Tower_Light` | buy | −40 | 0 |
| 4470 | (0,6) | `Tower_Light` | buy | −40 | 35 |
| 4620 | (3,4) | `Tower_Light` | buy | −40 | 5 |
| 5310 | (2,5) | `Tower_Light` | buy | −40 | 5 |
| 5580 | (1,2) | `Tower_Shadow` | buy | −40 | 45 |
| 5580 | (3,2) | `Tower_Shadow` | buy | −40 | 5 |
| 5970 | (1,3) | `Tower_Shadow` | buy | −40 | 5 |
| 6390 | (3,3) | `Tower_Light` | buy | −40 | 50 |
| 6390 | (6,5) | `Tower_Light` | buy | −40 | 10 |
| 6930 | (7,7) | `Tower_Shadow` | buy | −40 | 0 |
| 7410 | (7,5) | `Tower_Light` | buy | −40 | 0 |
| 7860 | (8,3) | `Tower_Shadow` | buy | −40 | 50 |
| 7860 | (9,2) | `Tower_Light` | buy | −40 | 10 |
| 8310 | (8,5) | `Tower_Shadow` | buy | −40 | 0 |
| 8790 | (6,1) | `Tower_Light` | buy | −40 | 0 |
| 9000 | (7,3) | `Tower_Shadow` | buy | −40 | 65 |
| 9000 | (7,1) | `Tower_Light` | buy | −40 | 25 |
| 9450 | (9,1) | `Tower_Shadow` | buy | −40 | 5 |
| 9900 | (0,3) | `Tower_Shadow` | buy | −40 | 5 |

**Building nothing:** wave 1 pays 40 on tick 1672 and wave 2 pays 20 on tick 2332, both fully leaked; the run is lost on the 2931st `Tick()` (48.9 s) with 175 gold, during wave 3, before any mixed wave.

## Sensitivities

Strategy cells show the outcome and lives left; crowd cells show wins in 40, then the median lives (range). Building nothing loses on tick 2931 in every row but the delay row (2572).

| Change | Reads, sensible | Reads, maze | Half and half L / S first | Reference order | Saver / late, reading | Sloppy reading | Sloppy half and half | Sloppy random | Sloppy 60/40 | Recoverable (sloppy reading) |
|---|---|---|---|---|---|---|---|---|---|---|
| Proposal | Won, 10 | Won, 10 | Won 9 / Won 7 | Won, 10 | Won 6 / Lost | 40, 9 (4–10) | 40, 8 (2–10) | 34, 8 (0–10) | 30, 5 (0–10) | 28% |
| **Bonus 0** | Lost | Lost | Lost / Lost | Lost | Lost / Lost | 0 | 0 | 0 | 0 | 24% |
| Bonus of step 4a (10 per wave) | Lost | Lost | Lost / Lost | Lost | Lost / Lost | 0 | 0 | 0 | 0 | 28% |
| Gold per kill 9 (−10%) | Won, 10 | Won, 10 | Won 10 / Won 5 | Lost | Lost / Lost | 37, 7 (0–10) | 31, 5 (0–10) | 21, 2 (0–10) | 25, 2 (0–9) | 28% |
| Gold per kill 8 (−20%) | Lost | Won, 7 | Lost / Won 4 | Lost | Lost / Lost | 15, 0 (0–10) | 11, 0 (0–9) | 9, 0 (0–7) | 4, 0 (0–2) | 29% |
| Gold per kill 12 (+20%) | Won, 10 | Won, 10 | Won 10 / Won 10 | Won, 10 | Won 6 / Won 7 | 40, 10 (7–10) | 40, 10 (8–10) | 37, 9 (0–10) | 38, 7 (0–10) | 28% |
| Health −1 hit (−20) in waves 4–10 | Won, 10 | Won, 10 | Won 10 / Won 10 | Won, 10 | Won 6 / Won 6 | 40, 10 (7–10) | 40, 10 (5–10) | 35, 9 (0–10) | 35, 7 (0–10) | 28% |
| Health +1 hit in waves 4–10 | Won, 10 | Won, 10 | Won 6 / Won 5 | Won, 4 | Lost / Lost | 33, 7 (0–10) | 34, 6 (0–10) | 20, 1 (0–10) | 19, 0 (0–10) | 29% |
| Health +2 hits in waves 4–10 | Lost | Won, 10 | Won 1 / Won 9 | Lost | Lost / Lost | 13, 0 (0–10) | 11, 0 (0–10) | 5, 0 (0–6) | 5, 0 (0–10) | 29% |
| Health +1 hit in waves 7–10 | Won, 10 | Won, 10 | Won 6 / Won 5 | Won, 3 | Won 4 / Lost | 38, 7 (0–10) | 35, 6 (0–10) | 24, 4 (0–10) | 22, 1 (0–10) | 29% |
| Health −1 hit in waves 9–10 | Won, 10 | Won, 10 | Won 10 / Won 10 | Won, 10 | Won 6 / Lost | 40, 10 (8–10) | 40, 10 (7–10) | 36, 9 (0–10) | 31, 7 (0–10) | 28% |
| **Cost 48 (+20%)** | Lost | Lost | Lost / Lost | Lost | Lost / Lost | 0 | 0 | 0 | 1, 0 (0–4) | 29% |
| Cost 32 (−20%; the start buys 3) | Won, 10 | Won, 10 | Won 10 / Won 10 | Won, 10 | Won 6 / Won 7 | 40, 10 | 40, 10 (8–10) | 39, 10 (0–10) | 40, 10 (8–10) | 27% |
| Starting gold 92 (−20%) | Won, 9 | Won, 10 | Won 10 / Won 5 | Won, 10 | Lost / Won 3 | 38, 9 (0–10) | 37, 8 (0–10) | 25, 3 (0–10) | 24, 2 (0–9) | 29% |
| Starting gold 138 (+20%; buys 3) | Won, 10 | Won, 10 | Won 10 / Won 10 | Won, 10 | Lost / Won 4 | 40, 10 (1–10) | 40, 10 (2–10) | 33, 9 (0–10) | 36, 8 (0–10) | 28% |
| Refund 40% / 60% | Won, 10 | Won, 10 | Won 9 / Won 7 | Won, 10 | Won 6 / Lost | 40, 9 (4–10) | 40, 8 (2–10) | 34, 8 (0–10) | 30, 5 (0–10) | 28% |
| Delay 5 s between all waves | Won, 10 | Won, 10 | Won 10 / Won 10 | Lost | Lost / Won 4 | 38, 10 (0–10) | 40, 8 (1–10) | 27, 6 (0–10) | 24, 3 (0–10) | 28% |
| Waves 4–10 mirrored (Light and Shadow swapped) | Won, 10 | Won, 10 | Won 7 / Won 9 | Lost | Won 2 / Lost | 40, 10 (4–10) | 40, 9 (5–10) | 33, 7 (0–10) | 32, 3 (0–10) | 28% |
| Waves 4–10 as two blocks each | Won, 10 | Won, 10 | Won 10 / Won 10 | Won, 7 | Lost / Won 1 | 40, 9 (2–10) | 40, 10 (5–10) | 29, 6 (0–10) | 23, 1 (0–8) | 6% |
| Waves 4–10 alternating one by one | Won, 10 | Won, 10 | Won 7 / Won 9 | Won, 7 | Won 6 / Lost | 39, 10 (0–10) | 40, 9 (3–10) | 35, 7 (0–10) | 30, 4 (0–10) | 31% |

The reference order is a fixed timetable, not a player: it loses wherever a purchase is no longer affordable on its tick, or, mirrored, where its towers have the wrong element. In every row, every bot that buys one element after wave 3 loses all 40 runs.

**Design targets met.**
1. **A one-element board leaks on a mixed wave and loses:** every bot that buys one element after wave 3 loses all 40 runs, and so does 70/30 in 38 of 40. A reader that stops mixing leaks 4 to 6 enemies on wave 7 or 8, the uneven waves.
2. **A mixed build holds:** half and half and the reader win 40 of 40. The losses come at waves 9 and 10, by amount, not by element.
3. **Winnable, not trivial:** a sloppy reader keeps a median of 9 lives (4 to 10) and a sloppy half-and-half player 8 (2 to 10). At 20% less starting gold the sloppy reader still wins 38 of 40, at 10% less kill gold 37. 60/40 wins 30 in 40 with a median of 5 lives, random elements 34 with 8.
4. **Both elements at once:** in waves 6 to 10 both elements share the board in 87 to 100% of the busy ticks, and 26 to 36% of their launches are recoverable (step 4b: 3% and none).

**Breaks first.**
1. **Tower cost +20%**: no bot wins. In a mixed wave each element only faces half the board, so the build cannot tilt towards the wave as in step 4b, and there is no slack for a weaker economy. Do not raise the cost or cut a bonus without lowering health.
2. **Health in waves 9 and 10**: +1 hit (+20) on waves 7 to 10 leaves the sloppy reader 38 wins and a median of 7, and +2 hits on all waves 13 wins. The margin is about one to two hits.
3. **Start late**: the bot that waits for wave 2 loses (in step 4b it won with 2 lives), because the 3 lives lost to wave 1 leave no room for the leaks of waves 8 and 9.

## Open points and risks

- **The talent "never attack the own element" is now worth a lot**: it would recover about 28% of all launches, 33 to 36% on waves 9 and 10. A run tuned without it becomes easy with it; size it when talents arrive.
- **Wasted shots on view.** In waves 6 to 10 a tower spends a third of its launches on an immune enemy that leads a damageable one. That is the rule and what this step is meant to show; if playtests read it as broken towers, the answer is the talent or the smarter targeting in the Parking lot, not the numbers.
- **The label matters less than in step 4b.** Reading and half and half win the same (40 vs 40 runs, median 9 vs 8), because the waves are close to even and the board persists. Only waves 7 and 8 reward reading. That fits a step about mixing; the announcement gains weight again with uneven or three-element scenarios.
- **Bots are not a player.** They place well (the sloppy human picks among the 6 best cells), never misread colours, never hesitate under pressure, and the reading bot weighs health exactly. A first-time player is probably weaker, especially at keeping the split near half and half: 60/40 already costs a median of 5 lives, and 70/30 loses. The simulation cannot tell whether players will see the mix in time from the label and the colours.
- **Tune first in play:** the health of waves 9 and 10 (by whole hits), then of wave 6 (the first stream: +15% there makes 1.1 enemies leak), then the order of the groups in wave 4 if the first mixed wave confuses. Every number is low to medium confidence.

Simulation: `tools/balance/`. `verify3c.py`, `verify4b.py` and `verify4e.py` check the port pins; `core.py` is the port; `proposal.py` holds these numbers (`proposal4b.py` the step 4b ones); `report4e.py main | waves | sens` prints the strategy and crowd tables, the per-wave table and the reference order, and the sensitivities; `refrepair.py 0 0` builds and checks the reference order (`ref4e.json`); `explore4e.py patterns | health | margin` gives the design search above.
