# Step 5a (XP and level, counters only): the proposal and the helpers shared by report5a.py and explore5a.py.
# XP changes no outcome, so each run is played once with the proposal's XP and levels in the port (core.py counts them),
# and other tables or XP schemes are evaluated from the run's kill log (tick, wave of the enemy) and its wave-bonus ticks.
# A wave's window runs from its first entry to the next wave's first entry (wave 10: to the run's end): the wave the HUD shows.
from multiprocessing import Pool
from p4 import *
from report4e import FIXED, CROWDS, one4e
import proposal as pr

# ---- the step 5a proposal ----
XP = 10                                                             # XP per enemy, every enemy asset
LEVELS = [30, 40, 70, 90, 110, 130, 150, 170, 190, 210]             # XP to go from level n to n + 1 (n = 1, 2, ...)
PROP5 = dict(pr.PROP, xp=XP, levels=LEVELS)


def with_xp(p, xp=XP, levels=LEVELS): return dict(p, xp=xp, levels=levels)


def cum(levels):
    out = []; s = 0
    for x in levels: s += x; out.append(s)
    return out


def entries(r): return [r['wave_start'][k][0] for k in sorted(r['wave_start'])]


def wave_of_tick(t, entry):
    """1..10: the wave whose window holds tick t; 0 before wave 1."""
    return sum(1 for e in entry if t >= e)


def events_of(r, xp=XP, wave_xp=None):
    """(tick, XP) events of a run: each kill pays xp (one number, or per wave of the enemy); wave_xp (optional, per wave) is paid on
    the tick the wave's gold bonus is paid (its last enemy has left the board, killed or not, the player alive)."""
    ev = [(t, xp[w] if isinstance(xp, (list, tuple)) else xp) for t, w in r['kill_log']]
    if wave_xp: ev += [(t, wave_xp[k]) for k, t in enumerate(r['paid_tick']) if t is not None and wave_xp[k]]
    return sorted(ev, key=lambda e: e[0])


def levelups(events, levels=LEVELS):
    """Level-up ticks: XP carries over, and the table caps the level."""
    c = cum(levels); out = []; xp = 0
    for t, x in events:
        xp += x
        while len(out) < len(c) and xp >= c[len(out)]: out.append(t)
    return out


def per_wave(lu, entry):
    """Level-ups in each wave's window, waves 1..10 (None for a window the run never reached)."""
    n = [0] * 11
    for t in lu: n[wave_of_tick(t, entry)] += 1
    return [n[k] if k <= len(entry) else None for k in range(1, 11)]


def level_at_ends(lu, r):
    """Level at the end of each wave's window (the tick before the next wave's first entry; wave 10: the run's end); None if not reached."""
    entry = entries(r); ends = [e - 1 for e in entry[1:]] + [r['end_tick']]
    return [1 + sum(1 for t in lu if t <= x) for x in ends] + [None] * (10 - len(ends))


def targets(r, levels=LEVELS, xp=XP, wave_xp=None):
    """The three pacing targets on one run: (first level-up by the end of wave 1 if wave 1 was fully killed, at most one level-up per
    window from wave 4 on, no two consecutive windows without one), plus the level-up count and the per-window counts."""
    lu = levelups(events_of(r, xp, wave_xp), levels); pw = per_wave(lu, entries(r)); seen = [x for x in pw if x is not None]
    w1 = sum(1 for t, w in r['kill_log'] if w == 0) == 3
    first = (not w1) or (bool(lu) and r['paid_tick'][0] is not None and lu[0] <= r['paid_tick'][0])
    return first, all(x <= 1 for x in seen[3:]), all(a + b > 0 for a, b in zip(seen, seen[1:])), len(lu), pw


def strategies(p=PROP5): return [(x[0], (p,) + x[1:]) for x in FIXED]
def crowds(p=PROP5, n=40): return [(name, [(p,) + f(s) for s in range(n)]) for name, f in CROWDS]


def play(pool, p=PROP5):
    """([(name, result)] of the fixed strategies, {crowd name: [results]}) on p."""
    fx = pool.map(one4e, [j for _, j in strategies(p)])
    return list(zip([n for n, _ in strategies(p)], fx)), {name: pool.map(one4e, jobs) for name, jobs in crowds(p)}
