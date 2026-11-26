# Prints every table and pin of step 5b (docs/balance/0001, "Step 5b"). Run verify5b.py (and verify3c/4b/4e.py) first for the port pins.
# python3 report5b.py main    the pool, the assets, building nothing, the reference order with its choices, strategies, crowds x pickers,
#                             the largest damage, two levels on one tick, range stacks
import sys, json, statistics, struct
from multiprocessing import Pool
from perks5b import *
import proposal5b as p5
from report4e import FIXED, NAME

REF = [(t, tuple(c), a, e) for t, c, a, e in json.load(open(os.path.join(HERE, 'ref5b.json')))]
SHOW = {LD: 'Light damage', SD: 'Shadow damage', RATE: 'Fire rate', RNG_: 'Range', GOLD_: 'Gold per kill'}
CROWDS = [('Sloppy human, reads', lambda s: ('sloppy', s, 'board', 0)), ('Sloppy human, half and half', lambda s: ('sloppy', s, 'halfL' if s % 2 else 'halfS', 0)),
          ('Sloppy human, random elements', lambda s: ('sloppy', s, 'random', s)), ('Sloppy human, 60/40', lambda s: ('sloppy', s, 'skewL' if s % 2 else 'skewS', 0.6)),
          ('Sloppy human, 70/30', lambda s: ('sloppy', s, 'skewL' if s % 2 else 'skewS', 0.7)),
          ('Sloppy human, one element after wave 3', lambda s: ('sloppy', s, 'thenL' if s % 2 else 'thenS', 0)),
          ('Sloppy human, one element', lambda s: ('sloppy', s, 'L' if s % 2 else 'S', 0))]
PK = ['reads', 'random', 'gold', 'majority', 'range', 'rate']
N = 40


def card(m):
    s, e, op, a = m[:4]
    return f"x{a:.2f} (`+{round((a - 1) * 100)} %`)" if op == MUL else f"+{a:g} (`+{a:g}`)"


def cell(rs):
    lv = sorted(r['lives'] for r in rs); n = len(rs); return f"{sum(r['out'] == 'Won' for r in rs)}, {lv[n // 2]} ({lv[0]}–{lv[-1]})"


def refjob(a): return run(dict(p5.PROP, seed=p5.SEED), Replay(REF, a), picker=picker('reads'))


if __name__ == '__main__':
    pool = Pool(17)
    print('pool, in order:'); [print(f'| {k} | {SHOW[k]} | {card(m)} |') for k, m in enumerate(p5.POOL)]
    print('waves 4-10:', [(pt, h, -(-h // 20)) for pt, h in p5.MIX])
    assets = pr.assets(mix=p5.MIX); old = pr.assets()
    print('enemy assets', len(assets), ', '.join(f'Enemy_{NAME[e]}{h}' for e, h in assets))
    print('  kept', [f'Enemy_{NAME[e]}{h}' for e, h in assets if (e, h) in old], 'new', [f'Enemy_{NAME[e]}{h}' for e, h in assets if (e, h) not in old],
          'deleted', [f'Enemy_{NAME[e]}{h}' for e, h in old if (e, h) not in assets])
    r = run(dict(p5.PROP, seed=p5.SEED), Nothing(), picker=picker('reads'))
    print(f"pin, builds nothing: {r['out']} on tick {r['end_tick']}, gold {r['gold']}, XP {r['xp']}, level {r['level']}, choices {len(r['choices'])}")
    ref = refjob(0)
    print(f"pin, reference order (seed {p5.SEED}): {ref['out']} on tick {ref['end_tick']}, lives {ref['lives']}, gold {ref['gold']}, kills {ref['kills']}, "
          f"towers {ref['towers']} ({ref['nL']} Light, {ref['nS']} Shadow), XP {ref['xp']}, level {ref['level']}, refused {ref['bad']}, largest hit {ref['max_dmg']}")
    print('  level-up ticks', ref['levelup_ticks'], 'taken', ref['taken'])
    acts = {t for t, *_ in REF}
    print('| Choice | Opens on tick | Level | Offer (slots 0, 1, 2) | Slot picked | Perk taken | Same tick as a purchase |')
    for i, (t, lvl, off, slot) in enumerate(ref['choices']):
        print(f"| {i + 1} | {t} | {lvl} | {', '.join(map(str, off))} ({', '.join(SHOW[k] for k in off)}) | {slot} | {off[slot]} {SHOW[off[slot]]} | {'yes' if t in acts else 'no'} |")
    res = pool.map(refjob, range(31))
    print('reference over shifts 0..30:', sorted({(x['out'], x['lives'], x['end_tick'], x['gold']) for x in res}), 'refusals', [x['bad'] for x in res if x['bad']])
    print('purchases:'); [print(f"| {x[0]} | {tuple(x[1])} | `Tower_{NAME[x[5]]}` | {x[2]} | {x[3]:+d} | {x[4]} |") for x in ref['log']]
    # strategies with the reading picker, and every crowd with every picker
    every = [ref, r]
    fx = pool.map(job, [(p5.PROP,) + j[1:] + ('reads', 0) for j in FIXED if j[1] != 'ref'])
    every += fx
    print('| Strategy (picks: reads) | Outcome | End tick | Lives | Kills | Light/Shadow | Gold at end | Level | Perks taken (0..4) |')
    for (name, *_), x in zip([j for j in FIXED if j[1] != 'ref'], fx):
        print(f"| {name} | {x['out']} | {x['end_tick']} | {x['lives']} | {x['kills']} | {x['nL']}/{x['nS']} | {x['gold']} | {x['level']} | {x['taken']} |")
    print('| Crowd (40 seeds) | ' + ' | '.join(PK) + ' |')
    ranges = {}
    for cn, fn in CROWDS:
        row = []
        for pk in PK:
            rs = pool.map(job, [(p5.PROP,) + fn(s) + (pk, s) for s in range(N)]); every += rs; row.append(cell(rs))
            ranges.setdefault(pk, []).extend(x['taken'][RNG_] for x in rs)
        print(f'| {cn} | ' + ' | '.join(row) + ' |', flush=True)
    print('range picks per run, share of runs with 0/1/2/3/4+ :', {pk: [round(100 * sum((v == k) if k < 4 else (v >= 4) for v in vs) / len(vs)) for k in range(5)] for pk, vs in ranges.items()})
    two = [x['levelup_ticks'] for x in every if len(set(x['levelup_ticks'])) != len(x['levelup_ticks'])]
    big = max(x['max_dmg'] for x in every)
    print(f'runs: {len(every)}; runs with two levels on one tick: {len(two)}; largest launch damage seen: {big}; most level-ups in a run: {max(len(x["levelup_ticks"]) for x in every)}')
    m = Modifiers()
    for _ in range(7): m.add(p5.POOL[LD], LD)
    d = m.apply(DMG, 'L', 10.0); print(f'largest damage any run can show: 10 x {p5.AMOUNTS["dmg"]}^7 = {d} per shot, x2 against the other element = {f(d * 2.0)}')
