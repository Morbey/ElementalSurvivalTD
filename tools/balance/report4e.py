# Prints every table of step 4e (mixed waves). Run verify3c.py, verify4b.py and verify4e.py first for the port pins.
# python3 report4e.py main | waves | sens
import json, sys
from multiprocessing import Pool
from p4 import *
import proposal as pr
from proposal import PROP, params, MIX, FIRST
REF = [(t, tuple(c), a, e) for t, c, a, e in json.load(open(os.path.join(HERE, 'ref4e.json')))]
TOWER = {'L': 'Tower_Light', 'S': 'Tower_Shadow'}
NAME = {'L': 'Light', 'S': 'Shadow'}


def one4e(a):
    p, kind, arg, e, earg = a
    if kind == 'ref': return run(p, Replay(REF, arg))
    return one(a)


def pct(a, b): return f"{100 * a / b:.0f}%" if b else '–'
def waste(r): return f"{pct(r['wasted'], r['shots'])} / {pct(r['recoverable'], r['shots'])}"
def row(name, r):
    return (f"| {name} | {r['out']} | tick {r['end_tick']}, {r['t']} s | {r['lives']} | {r['kills']} | {r['nL']}/{r['nS']} | {r['bonus']} | "
            f"{'/'.join(str(r['gold_at'].get(s, '–')) for s in (30, 60, 90, 120, 150))} | {r['gold']} | {waste(r)} | {pct(r['mixed'], r['busy'])} |")
def crowd(rs):
    lv = sorted(r['lives'] for r in rs); n = len(rs); won = [r for r in rs if r['out'] == 'Won']
    gold = sorted(r['gold'] for r in won)
    return (f"{len(won)}/{n} won | median {lv[n // 2]} ({lv[0]}–{lv[-1]}) | median gold {gold[len(gold) // 2] if gold else '–'} | "
            f"{pct(sum(r['wasted'] for r in rs), sum(r['shots'] for r in rs))} / {pct(sum(r['recoverable'] for r in rs), sum(r['shots'] for r in rs))} | "
            f"{pct(sum(r['mixed'] for r in rs), sum(r['busy'] for r in rs))}")
def short(rs):
    lv = sorted(r['lives'] for r in rs); n = len(rs); return f"{sum(r['out'] == 'Won' for r in rs)}, {lv[n // 2]} ({lv[0]}–{lv[-1]})"


FIXED = [('Builds nothing', 'none', 0, 'none', 0), ('Reads, sensible', 'sensible', 0, 'board', 0), ('Reads, maze', 'maze', 0, 'board', 0),
         ('Half and half, Light first', 'sensible', 0, 'halfL', 0), ('Half and half, Shadow first', 'sensible', 0, 'halfS', 0),
         ('Half and half, maze, Light first', 'maze', 0, 'halfL', 0), ('Half and half, maze, Shadow first', 'maze', 0, 'halfS', 0),
         ('Light only', 'sensible', 0, 'L', 0), ('Shadow only', 'sensible', 0, 'S', 0),
         ('Reads waves 1–3, then Light only', 'sensible', 0, 'thenL', 0), ('Reads waves 1–3, then Shadow only', 'sensible', 0, 'thenS', 0),
         ('Reads, sells 2 and rebuilds at wave 8', 'swap', (8, 2), 'board', 0),
         ('Late: first tower at wave 2, reads', 'late', 2, 'board', 0), ('Saver: 2 at start, then batches of 3, reads', 'saver', 3, 'board', 0),
         ('Reference order', 'ref', 0, None, 0)]
CROWDS = [('Sloppy human, reads (40 seeds)', lambda s: ('sloppy', s, 'board', 0)),
          ('Sloppy human, half and half (40 seeds)', lambda s: ('sloppy', s, 'halfL' if s % 2 else 'halfS', 0)),
          ('Sloppy human, random elements (40 seeds)', lambda s: ('sloppy', s, 'random', s)),
          ('Sensible, random elements (40 seeds)', lambda s: ('sensible', 0, 'random', s)),
          ('Sloppy human, 60/40 split (40 seeds)', lambda s: ('sloppy', s, 'skewL' if s % 2 else 'skewS', 0.6)),
          ('Sloppy human, 70/30 split (40 seeds)', lambda s: ('sloppy', s, 'skewL' if s % 2 else 'skewS', 0.7)),
          ('Sloppy human, reads waves 1–3, then one element (40 seeds)', lambda s: ('sloppy', s, 'thenL' if s % 2 else 'thenS', 0)),
          ('Sloppy human, one element (40 seeds)', lambda s: ('sloppy', s, 'L' if s % 2 else 'S', 0))]


def crowd_jobs(p, f, n=40): return [(p,) + f(s) for s in range(n)]


def wave_text(i):
    if i < 3: e, c, hp = FIRST[i]; return f"{NAME[e]} {c} × {hp}"
    pat, h = MIX[i - 3]; return ' + '.join(f"{c} {NAME[e]}" for e, c in pr.runs(pat)) + f" × {h}"


if __name__ == '__main__':
    pool = Pool(17); mode = sys.argv[1] if len(sys.argv) > 1 else 'main'
    if mode == 'main':
        print('assets:', ', '.join(f"Enemy_{NAME[e]}{h}" for e, h in pr.assets()), len(pr.assets()))
        b = Battle(mk_waves(PROP), 10, TS, gold=pr.S, cost=pr.C, refund_pct=pr.REFUND); g = b.gold; pays = []; entries = []
        while b.outcome == 'Running':
            n0 = sum(b.spawned); b.tick()
            if sum(b.spawned) > n0: entries.append(b.tick_n)
            if b.gold != g: pays.append((b.tick_n, b.gold - g)); g = b.gold
        print('builds nothing:', b.outcome, b.tick_n, 'gold', b.gold, 'bonus ticks', pays)
        b = Battle(mk_waves(PROP), 10 ** 6, TS); e = []
        while b.outcome == 'Running':
            n0 = sum(b.spawned); b.tick()
            if sum(b.spawned) > n0: e.append(b.tick_n)
        firsts = [e[sum(range(3, 3 + k))] for k in range(10)]
        print('entry ticks, first enemy of each wave:', firsts, 'last enemy:', e[-1], 'enemies:', len(e))
        rs = pool.map(one4e, [(PROP,) + j[1:] for j in FIXED])
        print('| Strategy | Outcome | End | Lives left | Kills | Towers Light/Shadow at end | Bonus | Gold at 30/60/90/120/150 s | Gold at end | Wasted / recoverable launches | Both elements on the board |')
        for (name, *_), r in zip(FIXED, rs): print(row(name, r))
        print('| Strategy | Won | Lives left | Gold at end (wins) | Wasted / recoverable launches | Both elements on the board |')
        for name, f in CROWDS: print(f"| {name} | {crowd(pool.map(one4e, crowd_jobs(PROP, f)))} |", flush=True)
        print('leaks per wave:'); [print('  ', j[0], r['leaks_w'], 'reached wave', r['wave_reached']) for j, r in zip(FIXED, rs)]
        ref = rs[-1]
        print('reference', ref['out'], ref['end_tick'], ref['t'], ref['lives'], ref['kills'], ref['gold'], ref['nL'], ref['nS'], 'first kill', ref['first_kill'], 'waste', waste(ref))
        for x in ref['log']: print('|', x[0], '|', tuple(x[1]), '|', f"`{TOWER[x[5]]}`", '|', x[2], '|', ('+' if x[3] > 0 else '−' if x[3] < 0 else '') + str(abs(x[3])), '|', x[4], '|')
        res = pool.map(one4e, [(PROP, 'ref', s, None, 0) for s in range(31)])
        print('reference over shifts 0..30:', sorted({(r['out'], r['lives'], r['end_tick'], r['gold'], r['nL'], r['nS'], r['first_kill']) for r in res}), 'refusals', [r['bad'] for r in res if r['bad']])
    if mode == 'waves':
        groups = [('Sloppy, reads', crowd_jobs(PROP, CROWDS[0][1])), ('Sloppy, half and half', crowd_jobs(PROP, CROWDS[1][1])),
                  ('Reference', [(PROP, 'ref', 0, None, 0)])]
        res = {n: pool.map(one4e, js) for n, js in groups}
        print('| Wave | Groups in order × health | Hits to kill | Bonus | ' + ' | '.join(f"{n}: recoverable / wasted, both on board, leaks" for n, _ in groups) + ' |')
        for k in range(10):
            hp = FIRST[k][2] if k < 3 else MIX[k - 3][1]
            cells = []
            for n, _ in groups:
                rs = res[n]; sh = sum(r['shots_w'][k] for r in rs)
                cells.append(f"{pct(sum(r['recov_w'][k] for r in rs), sh)} / {pct(sum(r['wasted_w'][k] for r in rs), sh)}, "
                             f"{pct(sum(r['mixed_w'][k] for r in rs), sum(r['busy_w'][k] for r in rs))}, {sum(r['leaks_w'][k] for r in rs) / len(rs):.1f}")
            print(f"| {k + 1} | {wave_text(k)} | {-(-hp // 20)} | {pr.BONUS[k]} | " + ' | '.join(cells) + ' |')
        for n, _ in groups:
            rs = res[n]; print(n, 'whole run: recoverable', pct(sum(r['recoverable'] for r in rs), sum(r['shots'] for r in rs)),
                               'wasted', pct(sum(r['wasted'] for r in rs), sum(r['shots'] for r in rs)), 'both', pct(sum(r['mixed'] for r in rs), sum(r['busy'] for r in rs)))
    if mode == 'sens':
        def hits(d): return params(mix=[(p_, h + 20 * d) for p_, h in MIX])
        def late(d): return params(mix=[(p_, h + (20 * d if j >= 3 else 0)) for j, (p_, h) in enumerate(MIX)])
        flip = lambda pat: pat.translate(str.maketrans('LS', 'SL'))
        cases = [('Proposal', PROP), ('Bonus 0', params(bonus=[0] * 10)), ('Bonus of step 4a (10 per wave)', params(bonus=[10] * 10)),
                 ('Gold per kill 9 (−10%)', params(g=9)), ('Gold per kill 8 (−20%)', params(g=8)), ('Gold per kill 12 (+20%)', params(g=12)),
                 ('Health −1 hit (−20) in waves 4–10', hits(-1)), ('Health +1 hit in waves 4–10', hits(1)), ('Health +2 hits in waves 4–10', hits(2)),
                 ('Health +1 hit in waves 7–10', late(1)), ('Health −1 hit in waves 7–10', late(-1)), ('Health −1 hit in waves 9–10', params(mix=[(p_, h - (20 if j >= 5 else 0)) for j, (p_, h) in enumerate(MIX)])),
                 ('Cost 48 (+20%)', params(C=48)), ('Cost 32 (−20%)', params(C=32)), ('Starting gold 92 (−20%)', params(S=92)), ('Starting gold 138 (+20%)', params(S=138)),
                 ('Refund 40%', params(refund_pct=40)), ('Refund 60%', params(refund_pct=60)), ('Delay 5 s between all waves', params(delays=[15] + [5] * 9)),
                 ('Waves 4–10 mirrored (Light and Shadow swapped)', params(mix=[(flip(p_), h) for p_, h in MIX])),
                 ('Waves 4–10 as two blocks each', params(mix=[(''.join(sorted(p_, key=lambda c: c != p_[0])), h) for p_, h in MIX])),
                 ('Waves 4–10 alternating one by one', params(mix=[(''.join('SL'[(j + i) % 2] for i in range(len(p_))), h) for j, (p_, h) in enumerate(MIX)]))]
        print('| Change | Builds nothing | Reads, sensible | Reads, maze | Half and half L / S first | Reference order | Saver / late first tower, reading | Sloppy humans reading | Sloppy half and half | Sloppy random elements | Sloppy 60/40 | Sloppy one element after wave 3 | Recoverable launches (sloppy reading) |')
        for name, p in cases:
            r = pool.map(one4e, [(p, 'none', 0, 'none', 0), (p, 'sensible', 0, 'board', 0), (p, 'maze', 0, 'board', 0), (p, 'sensible', 0, 'halfL', 0), (p, 'sensible', 0, 'halfS', 0), (p, 'ref', 0, None, 0),
                                  (p, 'saver', 3, 'board', 0), (p, 'late', 2, 'board', 0)])
            f_ = lambda x: f"{x['out']}, {x['lives']}"
            cr = [pool.map(one4e, crowd_jobs(p, CROWDS[i][1])) for i in (0, 1, 2, 4, 6)]
            print(f"| {name} | {r[0]['out']} tick {r[0]['end_tick']} | {f_(r[1])} | {f_(r[2])} | {r[3]['out'][0]}{r[3]['lives']} / {r[4]['out'][0]}{r[4]['lives']} | {f_(r[5])} | {r[6]['out'][0]}{r[6]['lives']} / {r[7]['out'][0]}{r[7]['lives']} | "
                  + ' | '.join(short(c) for c in cr) + f" | {pct(sum(x['recoverable'] for x in cr[0]), sum(x['shots'] for x in cr[0]))} |", flush=True)
