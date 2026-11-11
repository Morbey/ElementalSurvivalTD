# Prints every table of the step 4b draft (Light and Shadow, wave bonus). Run verify3c.py first for the port pins.
import json, sys
from multiprocessing import Pool
from p4 import *
from proposal import *
from grid2 import agg
REF = [(t, tuple(c), a, e) for t, c, a, e in json.load(open(os.path.join(HERE, 'ref4_w8_k2.json')))]
TOWER = {'L': 'Tower_Light', 'S': 'Tower_Shadow'}
def one4b(a):
    p, kind, arg, e, earg = a
    if kind == 'ref': bot = Replay(REF, arg); return run(p, bot)
    return one(a)
def ws(r): return ' '.join(f"{v[4]}/{v[5]}" for k, v in sorted(r['wave_start'].items()))
def waste(r): return f"{100 * r['wasted'] / max(1, r['shots']):.0f}% ({r['recoverable']})"
def row(name, r):
    return (f"| {name} | {r['out']} | tick {r['end_tick']}, {r['t']} s | {r['lives']} | {r['kills']} | {r['nL']}/{r['nS']} | {r['bonus']} | "
            f"{'/'.join(str(r['gold_at'].get(s, '-')) for s in (30, 60, 90, 120, 150))} | {r['gold']} | {waste(r)} | {ws(r)} |")
def many(pool, jobs):
    rs = pool.map(one4b, jobs); lv = sorted(r['lives'] for r in rs); n = len(rs)
    return rs, (f"{sum(r['out'] == 'Won' for r in rs)}/{n} win, median {lv[n // 2]} ({lv[0]}–{lv[-1]}); waste "
                f"{100 * sum(r['wasted'] for r in rs) / sum(r['shots'] for r in rs):.0f}%, recoverable {sum(r['recoverable'] for r in rs)}; "
                f"both elements on the board {100 * sum(r['mixed'] for r in rs) / sum(r['busy'] for r in rs):.0f}% of busy ticks")
def sloppy(p, ep='board', earg=0, n=40): return [(p, 'sloppy', s, ep, earg) for s in range(n)]
if __name__ == '__main__':
    pool = Pool(17); mode = sys.argv[1] if len(sys.argv) > 1 else 'all'
    if mode in ('all', 'main'):
        b = Battle(mk_waves(PROP), 10, TS, gold=115, cost=40, refund_pct=50); g = b.gold; pays = []
        while b.outcome == 'Running':
            b.tick()
            if b.gold != g: pays.append((b.tick_n, b.gold - g)); g = b.gold
        print('builds nothing:', b.outcome, b.tick_n, 'gold', b.gold, 'bonus ticks', pays)
        jobs = [('Builds nothing', (PROP, 'none', 0, 'none', 0)), ('Reads, sensible cells', (PROP, 'sensible', 0, 'board', 0)),
                ('Reads, maze', (PROP, 'maze', 0, 'board', 0)), ('Reads, sells 2 Light and rebuilds Shadow when wave 8 enters', (PROP, 'swap', (8, 2), 'board', 0)),
                ('Reads, sells 2 Shadow and rebuilds Light when wave 9 enters', (PROP, 'swap', (9, 2), 'board', 0)),
                ('Light only', (PROP, 'sensible', 0, 'L', 0)), ('Shadow only', (PROP, 'sensible', 0, 'S', 0)),
                ('Half and half, Light first', (PROP, 'sensible', 0, 'halfL', 0)), ('Half and half, Shadow first', (PROP, 'sensible', 0, 'halfS', 0)),
                ('Half and half, maze, Light first', (PROP, 'maze', 0, 'halfL', 0)), ('Half and half, maze, Shadow first', (PROP, 'maze', 0, 'halfS', 0)),
                ('Random elements, seed 0', (PROP, 'sensible', 0, 'random', 0)),
                ('Late: first tower at wave 2', (PROP, 'late', 2, 'board', 0)), ('Late: first tower at wave 3', (PROP, 'late', 3, 'board', 0)),
                ('Saver: 2 at start, then batches of 3', (PROP, 'saver', 3, 'board', 0)), ('Reference order', (PROP, 'ref', 0, None, 0))]
        rs = pool.map(one4b, [j for _, j in jobs])
        print('| Strategy | Outcome | End | Lives | Kills | Towers L/S at end | Bonus | Gold at 30/60/90/120/150 s | Gold at end | Wasted shots (recoverable) | Towers L/S at waves 1-10 start |')
        for (name, _), r in zip(jobs, rs): print(row(name, r))
        print('leaks per wave:'); [print('  ', name, r['leaks_w']) for (name, _), r in zip(jobs, rs)]
        for name, js in (('sloppy reads', sloppy(PROP)), ('sloppy half', [(PROP, 'sloppy', s, 'halfL' if s % 2 else 'halfS', 0) for s in range(40)]),
                         ('sloppy random elements', [(PROP, 'sloppy', s, 'random', s) for s in range(40)]),
                         ('sensible random elements', [(PROP, 'sensible', 0, 'random', s) for s in range(40)]),
                         ('sloppy Light only', sloppy(PROP, 'L', 0, 10))):
            r_, txt = many(pool, js); print(name, txt, 'end s', min(r['t'] for r in r_), max(r['t'] for r in r_))
        ref = rs[-1]; print('reference', ref['out'], ref['end_tick'], ref['lives'], ref['gold'], ref['nL'], ref['nS'], 'first kill', ref['first_kill'], 'waste', waste(ref), 'mixed', ref['mixed'], ref['busy'])
        for e in ref['log']: print('|', e[0], '|', e[1], '|', TOWER[e[5]], '|', e[2], '|', ('+' if e[3] > 0 else '') + str(e[3]), '|', e[4], '|')
        res = pool.map(one4b, [(PROP, 'ref', s, None, 0) for s in range(31)])
        print('reference over shifts 0..30:', sorted({(r['out'], r['lives'], r['end_tick'], r['gold'], r['nL'], r['nS'], r['first_kill']) for r in res}), 'refusals', [r['bad'] for r in res if r['bad']])
        b = Battle(mk_waves(PROP), 10, TS, gold=S, cost=C); print('start buys', b.place((11, 2), 'L'), b.place((11, 3), 'S'), b.place((12, 2), 'L'), 'gold left', b.gold)
    if mode in ('all', 'sens'):
        def strict(o): return params(order=o)
        cases = [('Proposal', PROP), ('Bonus 0', params(bonus=[0] * 10)), ('Bonus of step 4a (10 per wave)', params(bonus=[10] * 10)),
                 ('Gold per kill 9 (-10%)', params(g=9)), ('Gold per kill 8 (-20%)', params(g=8)), ('Gold per kill 8, bonus 0', params(g=8, bonus=[0] * 10)),
                 ('Gold per kill 0', params(g=0)), ('Gold per kill 12 (+20%)', params(g=12)),
                 ('Health +20% in waves 1-3', params(hp=[int(round(h * (1.2 if i < 3 else 1) / 5)) * 5 for i, h in enumerate(HP)])),
                 ('Health +10% in waves 7-10', params(hp=up(HP, 1.1))), ('Health +20% in waves 7-10', params(hp=up(HP, 1.2))),
                 ('Health +20% in waves 7-10, bonus 0', params(hp=up(HP, 1.2), bonus=[0] * 10)), ('Health -20% in waves 7-10', params(hp=up(HP, 0.8))),
                 ('Cost 48 (+20%)', params(C=48)), ('Cost 32 (-20%)', params(C=32)), ('Starting gold 92 (-20%)', params(S=92)),
                 ('Starting gold 138 (+20%)', params(S=138)), ('Refund 40%', params(refund_pct=40)), ('Refund 60%', params(refund_pct=60)),
                 ('Strict alternation SLSLSLSLSL', strict('SLSLSLSLSL')), ('Strict alternation LSLSLSLSLS', strict('LSLSLSLSLS')),
                 ('Mirror LSSLSLLSLL', strict('LSSLSLLSLL')),
                 ('Delay 12 s before a wave of the other element', params(delays=[15] + [12 if ORDER[i] != ORDER[i - 1] else 8 for i in range(1, 10)])),
                 ('Delay 5 s between all waves', params(delays=[15] + [5] * 9))]
        print('| Change | Builds nothing | Reads, sensible | Reads, maze | Reads, swaps at wave 8 | Reference | Half and half L / S first | Sloppy humans reading (40) | Sloppy half and half (40) | Random elements (40) |')
        for name, p in cases:
            r = pool.map(one4b, [(p, 'none', 0, 'none', 0), (p, 'sensible', 0, 'board', 0), (p, 'maze', 0, 'board', 0), (p, 'swap', (8, 2), 'board', 0),
                                 (p, 'ref', 0, None, 0), (p, 'sensible', 0, 'halfL', 0), (p, 'sensible', 0, 'halfS', 0)])
            f_ = lambda x: f"{x['out']} {x['t']} s, {x['lives']}"
            _, h = many(pool, sloppy(p)); _, hh = many(pool, [(p, 'sloppy', s, 'halfL' if s % 2 else 'halfS', 0) for s in range(40)])
            _, rd = many(pool, [(p, 'sensible', 0, 'random', s) for s in range(40)])
            print(f"| {name} | {r[0]['out']} tick {r[0]['end_tick']}, gold {r[0]['gold']} | {f_(r[1])} | {f_(r[2])} | {f_(r[3])} | {f_(r[4])} | {r[5]['out'][0]}{r[5]['lives']} / {r[6]['out'][0]}{r[6]['lives']} | {h} | {hh} | {rd} |", flush=True)
    if mode in ('all', 'ahead'):
        for n in (1, 2, 3, 10):
            for name, p in (('proposal', PROP), ('gold per kill 8', params(g=8)), ('late health +20%', params(hp=up(HP, 1.2)))):
                rs = pool.map(one4b, [(p, 'sensible', 0, 'ahead', n), (p, 'maze', 0, 'ahead', n)])
                _, h = many(pool, sloppy(p, 'ahead', n)); print('ahead', n, name, [short(r) for r in rs], h, flush=True)
