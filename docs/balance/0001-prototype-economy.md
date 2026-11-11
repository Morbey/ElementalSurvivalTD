# 0001 – Prototype economy (slice 3b)

Provisional until playtests. Simulated on a float32 port of Core that reproduces the 3a pins (same wave entry ticks, last entry on tick 9120, both outcomes within one tick). Rules as in plan 0005: gold is checked before the board, a kill pays on the tick its shot lands, "fired" means a launch, and the refund is `cost * percent / 100` in integers.

## Values

| Value | Where | Number | Confidence |
|---|---|---|---|
| Starting gold | `Run_Prototype.startingGold` | 100 (buys exactly 2 towers; the 3rd is `NotEnoughGold`) | medium |
| Tower cost | `Tower_Prototype.cost` | 50 | medium |
| Gold per kill | `goldReward` on every enemy asset | 10 (5 kills per tower) | medium |
| Sell refund after the first launch | `Run_Prototype.sellRefundPercent` | 50 | low |
| Lives | `Run_Prototype.lives` | 10 (unchanged) | medium |
| Tower combat stats, enemy speed | `Tower_Prototype`, all enemies | unchanged (range 2.5, damage 10, 1.5 shots/s, projectile 10; speed 2) | – |

| Wave | Count | Interval s | Delay s | Enemy health | Enemy asset |
|---|---|---|---|---|---|
| 1 | 3 | 1 | 15 | 45 | `Enemy_Health45` (new) |
| 2 | 4 | 1 | 8 | 55 | `Enemy_Health55` (new) |
| 3 | 5 | 1 | 8 | 65 | `Enemy_Health65` (new) |
| 4 | 6 | 1 | 8 | 75 | `Enemy_Health75` (new) |
| 5 | 7 | 1 | 8 | 90 | `Enemy_Health90` (new) |
| 6 | 8 | 1 | 8 | 100 | `Enemy_Prototype` (unchanged, reward 10) |
| 7 | 9 | 1 | 8 | 115 | `Enemy_Health115` (new) |
| 8 | 10 | 1 | 8 | 130 | `Enemy_Health130` (new) |
| 9 | 11 | 1 | 8 | 150 | `Enemy_Health150` (new) |
| 10 | 12 | 1 | 8 | 175 | `Enemy_Health175` (new) |

Every new asset: speed 2, reward 10. Counts, intervals and delays are those of 3a. Confidence in the curve: low to medium, because it sits about 10% below a cliff (see Sensitivities).

## Strategies

Bots act every 0.5 s unless stated. "Sensible": each tower on the free cell next to the route (not on it) that covers most route cells. "Strong": an expert maze order found offline (two-step lookahead). "Sloppy human": acts every 2 s and takes a seeded-random cell among the 6 best sensible ones (40 seeds). Times count `Tick()` calls including the deciding one.

| Strategy | Outcome | End | Lives left | Kills | Towers at end | Gold at 30/60/90/120/150 s | Towers at waves 1–10 start |
|---|---|---|---|---|---|---|---|
| Builds nothing | Lost | tick 3089, 51.5 s | 0 | 0 | 0 | 100/–/–/–/– | 0 0 0 0 |
| Sensible, reinvests at once | Won | tick 9674, 161.2 s | 10 | 75 | 16 | 0/30/30/40/20 | 2 2 3 4 5 7 8 10 12 14 |
| Strong, mazes, reinvests at once | Won | tick 9688, 161.5 s | 10 | 75 | 16 | 0/30/20/30/10 | 2 2 3 4 5 7 8 10 12 14 |
| Sloppy human (40 seeds) | 36 won, 4 lost (at 80.5, 94.5, 111.5, 111.5 s) | 158–163 s if won | median 7 (0–10) | – | – | – | – |
| Bad: 2 towers far from the route | Lost | 51.5 s | 0 | 0 | 2 | 0/–/–/–/– | 2 2 2 2 |
| Bad: towers at the edge of range (≤ 2 route cells each) | Lost | 51.5 s | 0 | 0 | 2 | 0/–/–/–/– | 2 2 2 2 |
| Saves: first tower at wave 2 | Lost | 64.5 s | 0 | 7 | 3 | 20/10/–/–/– | 0 0 2 2 3 |
| Saves: first tower at wave 3 | Lost | 53.5 s | 0 | 3 | 2 | 100/–/–/–/– | 0 0 0 2 |
| Saves: 2 at start, then buys only in batches of 3 | Lost | 66.5 s | 0 | 9 | 2 | 50/80/–/–/– | 2 2 2 2 2 |
| Sensible, sells its first 2 towers at wave 5 and rebuilds them in the middle | Won | 159.6 s | 10 | 75 | 15 | 0/30/20/40/10 | 2 2 3 4 5 6 7 9 11 13 |

Route coverage and spare capacity ("capacity": the highest health the build kills with no leak, for that wave's count):

| Build, at the start of | Towers | Route cells in range | Capacity vs wave health |
|---|---|---|---|
| Strong, wave 6 / 8 / 10 | 7 / 10 / 14 | 25/34, 30/36, 33/38 | 117/100, 167/130, 230/175 |
| Sensible, wave 6 / 8 / 10 | 7 / 10 / 14 | 17/28, 28/28, 28/28 | 117/100, 159/130, 218/175 |

Five towers put every cell of the empty-board route in range.

## Reference build order (wins)

Each action runs after the given number of `Tick()` calls, before the next one. Every buy is affordable and placed. **Outcome: Won on the 9577th `Tick()` (159.6 s), 10 lives, 50 gold left.** The same outcome holds with every action after tick 0 shifted 1 to 29 ticks later. The first kill pays 10 on tick 988.

| Tick | Cell | Action | Gold change | Gold after |
|---|---|---|---|---|
| 0 | (0,8) | buy | −50 | 50 |
| 0 | (0,8) | sell, never fired | +50 | 100 |
| 0 | (2,5) | buy | −50 | 50 |
| 0 | (3,5) | buy | −50 | 0 |
| 1740 | (3,6) | buy | −50 | 0 |
| 2460 | (6,6) | buy | −50 | 0 |
| 3210 | (7,6) | buy | −50 | 0 |
| 3930 | (8,5) | buy | −50 | 0 |
| 4350 | (2,5) | sell, fired | +25 | 75 |
| 4350 | (3,5) | sell, fired | +25 | 100 |
| 4350 | (9,3) | buy | −50 | 50 |
| 4350 | (8,2) | buy | −50 | 0 |
| 5280 | (6,5) | buy | −50 | 0 |
| 5820 | (7,5) | buy | −50 | 0 |
| 6240 | (8,3) | buy | −50 | 0 |
| 6840 | (8,6) | buy | −50 | 0 |
| 7260 | (9,2) | buy | −50 | 0 |
| 7890 | (2,5) | buy | −50 | 0 |
| 8280 | (3,5) | buy | −50 | 0 |
| 8820 | (13,2) | buy | −50 | 0 |
| 9300 | (14,2) | buy | −50 | 0 |

Building nothing: Lost on the 3089th `Tick()` (51.5 s, the third enemy of wave 3), with 100 gold unspent. The 3a pin of 51.465 s for this run counted one tick fewer, so tests should allow ±1 tick.

## Sensitivities (one number changed by about ±20%)

| Change | Sensible bot | Sloppy humans (20 seeds) |
|---|---|---|
| Proposal | Won, 10 lives | 18/20 win, median 8 lives |
| Gold per kill 9 / 8 | Lost 80.5 s / 77.5 s | 1/20 / 0/20 win |
| Gold per kill 12 | Won, 10 | 20/20, median 9 |
| Tower cost 55 or 60, or starting gold 80 (1 starting tower) | Lost 52–67 s | 0/20 |
| Tower cost 40 / starting gold 120 | Won, 10 | 20/20, median 10 |
| Health ×1.05 / ×1.1 / ×1.2 | Won / Won / Lost 66.5 s | 12/20 / 12/20 / 0/20 |
| Health ×1.2 on waves 1–3 only / 7–10 only | Lost 78.5 s / Won | 5/20 / 12/20 |
| Health ×0.9 / ×0.8 | Won, 10 | 20/20, median 10 (×0.8: first tower at wave 3 also wins) |
| Refund 40% / 60% | Won, 10 (moving 2 towers costs 60 / 40 gold) | 18/20, median 8 |
| Lives 20 | Won, 20 | 20/20, median 18; building nothing loses at 75.5 s |

Breaks first: (1) the starting gold must buy 2 towers: 1 tower never starts the income; (2) gold per kill, where 10% less loses nearly every run; (3) the health of waves 1 to 3. The refund changes no outcome in the simulation; 50% makes moving a tower cost 2.5 kills.

Simulation: `/private/tmp/claude-501/-Users-morbey-Projetos-ElementalTD/c8be6820-3fc0-435a-a9af-5180aea801b2/scratchpad/sim3b/` (`report.py` prints every table, `replay.py` checks the reference order, `core.py` is the Core port).
