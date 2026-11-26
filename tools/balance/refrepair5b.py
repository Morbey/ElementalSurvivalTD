# Step 5b reference order: the 4e construction (refrepair.py: the maze order, reading elements, a tick-0 buy and sell, no swap) played on the
# 5b proposal with the reading picker under a seed, then repaired so that every shift of 0..30 ticks (all actions after tick 0) has no
# refused action. The table holds the purchases; the picks are the reading picker's on that seed, listed by report5b.py.
# python3 refrepair5b.py seeds N   ranks seeds 0..N-1 (won, lives, distinct perks taken, a perk taken twice, a range pick, robust to shifts)
# python3 refrepair5b.py <seed>    writes ref5b.json
import json, sys
from multiprocessing import Pool
from econ import *
from p4 import mkep
from refrepair import RefMaze
import proposal5b as p5
from perks5b import picker


def table_of(r): return [(t, c, a, e) for (t, c, a, _, _, e) in r['log']]


def replay(table, seed, shift=0): return run(dict(p5.PROP, seed=seed), Replay(table, shift), picker=picker('reads'))


def shifted(a):
    table, s, seed = a; r = replay(table, seed, s)
    return s, r['out'], r['lives'], r['end_tick'], r['gold'], r['nL'], r['nS'], r['first_kill'], r['bad']


def repair(table, seed, pool, rounds=80):
    """The first action refused under any shift moves 30 ticks later; repeat until no shift refuses one."""
    for _ in range(rounds):
        res = pool.map(shifted, [(table, s, seed) for s in range(31)])
        refused = [(x[8][0][0] - (x[0] if x[8][0][0] else 0), x[8][0][1]) for x in res if x[8]]
        if not refused: break
        t0, c0 = min(refused)
        i = next(i for i, (t, c, a, e) in enumerate(table) if c == c0 and t0 - 30 <= t <= t0 + 30 and t > 0)
        t, c, a, e = table[i]
        table = sorted(table[:i] + [(t + 30, c, a, e)] + table[i + 1:], key=lambda x: x[0])
    return table, res


def raw(seed):
    bot = RefMaze(8, 0); bot.ep = mkep('board')
    return run(dict(p5.PROP, seed=seed), bot, picker=picker('reads'))


def rank(seed):
    r = raw(seed); tk = r['taken']
    return seed, r['out'], r['lives'], sum(1 for x in tk if x), max(tk), tk, len(r['choices'])


if __name__ == '__main__':
    pool = Pool(17)
    if sys.argv[1] == 'seeds':
        rs = pool.map(rank, range(int(sys.argv[2])))
        good = sorted((x for x in rs if x[1] == 'Won'), key=lambda x: (-x[2], -x[3], -(x[4] >= 2), x[0]))
        print('won', len(good), 'of', len(rs)); [print(x) for x in good[:25]]
    else:
        seed = int(sys.argv[1]); r = raw(seed); table = table_of(r)
        print('raw:', r['out'], r['lives'], r['end_tick'], r['nL'], r['nS'], 'taken', r['taken'])
        table, res = repair(table, seed, pool)
        print('repaired, over shifts 0..30 (outcome, lives, end tick, gold, L, S, first kill):', sorted({x[1:8] for x in res}))
        print('shifts with refusals:', [x[0] for x in res if x[8]], 'shifts lost:', [x[0] for x in res if x[1] != 'Won'])
        json.dump(table, open(os.path.join(HERE, 'ref5b.json'), 'w'))
