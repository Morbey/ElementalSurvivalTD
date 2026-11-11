# Prints every table of the step 4a balance document (wave bonus, neutral towers and enemies, 3c health).
import json, sys
from multiprocessing import Pool
from p4 import *
from proposal import up
from grid2 import agg
H = [45, 55, 65, 75, 90, 100, 130, 175, 230, 305]; N = [None] * 10; BON = [10] * 10
def P(hp=H, bonus=BON, **kw): return params4(hp, N, bonus, **kw)
PROP = P()
REF = [(t, tuple(c), a) for t, c, a in json.load(open(os.path.join(HERE, 'ref3c.json')))]
def one4a(a):
    p, kind, arg = a
    if kind == 'ref': bot = Replay(REF, arg)
    elif kind == 'mover': bot = Mover(arg)
    elif kind == 'poor': bot = Poor()
    else: bot = mk(kind, arg)
    return run(p, bot, ep=NoElem())
def row(r):
    ws = r['wave_start']
    return (f"| {r['bot']} | {r['out']} | tick {r['end_tick']}, {r['t']} s | {r['lives']} | {r['kills']} | {r['towers']} | {r['bonus']} | "
            f"{'/'.join(str(r['gold_at'].get(s, '-')) for s in (30, 60, 90, 120, 150))} | {r['gold']} | {' '.join(str(ws[w][1]) for w in sorted(ws))} | {r['route']} |")
def humans(pool, p, n=40):
    rs = pool.map(one4a, [(p, 'sloppy', s) for s in range(n)]); lv = sorted(r['lives'] for r in rs)
    return f"{sum(r['out'] == 'Won' for r in rs)}/{n} win, median {lv[n // 2]} ({lv[0]}-{lv[-1]})"
if __name__ == '__main__':
    pool = Pool(17)
    b = Battle(mk_waves(PROP), 10, TS, gold=115, cost=40, refund_pct=50); g = b.gold; pays = []
    while b.outcome == 'Running':
        b.tick()
        if b.gold != g: pays.append((b.tick_n, b.gold - g)); g = b.gold
    print('builds nothing:', b.outcome, b.tick_n, 'gold', b.gold, 'bonus ticks', pays)
    b = Battle(mk_waves(PROP), 10, TS, gold=115, cost=40); print('start buys', b.place((11, 2)), b.place((11, 3)), b.place((12, 2)), 'gold left', b.gold)
    for name, p in (('proposal', PROP), ('bonus 0 (3c)', P(bonus=[0] * 10))):
        print('##', name)
        rs = pool.map(one4a, [(p, k, a) for k, a in [('none', 0), ('sensible', 0), ('maze', 0), ('far', 0), ('poor', 0), ('late', 2), ('late', 3),
                                                      ('late', 4), ('saver', 3), ('mover', 5), ('ref', 0)]])
        for r in rs: print(row(r))
        print('sloppy humans:', humans(pool, p))
    res = pool.map(one4a, [(PROP, 'ref', s) for s in range(31)])
    print('reference over shifts 0..30:', sorted({(r['out'], r['lives'], r['end_tick'], r['gold'], r['towers'], r['first_kill']) for r in res}), 'refusals', [r['bad'] for r in res if r['bad']])
    r = res[0]
    for e in r['log']: print('|', e[0], '|', e[1], '|', e[2], '|', ('+' if e[3] > 0 else '') + str(e[3]), '|', e[4], '|')
    cases = [('Proposal', PROP), ('Bonus 0 (the 3c numbers)', P(bonus=[0] * 10)), ('Bonus 5', P(bonus=[5] * 10)), ('Bonus 15', P(bonus=[15] * 10)),
             ('Gold per kill 9 (-10%)', P(g=9)), ('Gold per kill 8 (-20%)', P(g=8)), ('Gold per kill 8, bonus 0', P(g=8, bonus=[0] * 10)),
             ('Gold per kill 0 (bonus only)', P(g=0)), ('Gold per kill 12 (+20%)', P(g=12)),
             ('Health +20% in waves 1-3', P(hp=[int(round(h * (1.2 if i < 3 else 1) / 5)) * 5 for i, h in enumerate(H)])),
             ('Health +10% in waves 7-10', P(hp=up(H, 1.1))), ('Health +20% in waves 7-10', P(hp=up(H, 1.2))),
             ('Health +20% in waves 7-10, bonus 0', P(hp=up(H, 1.2), bonus=[0] * 10)), ('Health +30% in waves 7-10', P(hp=up(H, 1.3))),
             ('Health -20% in waves 7-10', P(hp=up(H, 0.8))), ('Cost 48 (+20%)', P(C=48)), ('Cost 32 (-20%)', P(C=32)),
             ('Starting gold 92 (-20%)', P(S=92)), ('Starting gold 138 (+20%)', P(S=138)), ('Refund 40%', P(refund_pct=40)), ('Refund 60%', P(refund_pct=60))]
    print('| Change | Builds nothing | Sensible | Maze | Sells and rebuilds | Reference order | Sloppy humans (40 seeds) |')
    for name, p in cases:
        r = pool.map(one4a, [(p, 'none', 0), (p, 'sensible', 0), (p, 'maze', 0), (p, 'mover', 5), (p, 'ref', 0)])
        f_ = lambda x: f"{x['out']} {x['t']} s, {x['lives']}"
        print(f"| {name} | {r[0]['out']} tick {r[0]['end_tick']}, gold {r[0]['gold']} | {f_(r[1])} | {f_(r[2])} | {f_(r[3])} | {f_(r[4])}, gold {r[4]['gold']} | {humans(pool, p)} |", flush=True)
