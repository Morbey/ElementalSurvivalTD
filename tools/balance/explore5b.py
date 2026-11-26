# Step 5b design search (docs/balance/0001, "Step 5b"). Seeded: crowd member s uses sloppy seed s, picker seed s and draw seed s.
# Worth is measured on the 4e waves (no retune), as the health multiplier on waves 4-10 that each run still wins, by bisection.
# python3 explore5b.py worth      one pick of one perk at level 2, 4, 6 or 8 (a no-op perk at the other levels)
# python3 explore5b.py stack      seven picks of one perk (damage aimed where the next wave needs it)
# python3 explore5b.py range      range picks 1 to 7 (+1 and +0.5) at the first levels, then nothing: the worth of each further pick
# python3 explore5b.py disc       the range disc against the board: route cells and wall sections a tower reaches, per range
# python3 explore5b.py margin [h4,...,h10 ...]   on each candidate, the health multiplier each run still holds, per crowd and picker (POOL as retune)
# python3 explore5b.py retune [h4,h5,...,h10 ...]   candidate healths of waves 4-10 against the design targets, on the proposal's pool
#                                                   (POOL=dmg,rate,range,gold in the environment tries other amounts)
import sys, os, statistics, math
from multiprocessing import Pool
from perks5b import *
import proposal5b as p5

N = 40
CROWDS = {'sloppy reads': lambda s: ('sloppy', s, 'board', 0), 'sloppy half': lambda s: ('sloppy', s, 'halfL' if s % 2 else 'halfS', 0),
          'sloppy random el.': lambda s: ('sloppy', s, 'random', s), 'sloppy 60/40': lambda s: ('sloppy', s, 'skewL' if s % 2 else 'skewS', 0.6),
          'sloppy 70/30': lambda s: ('sloppy', s, 'skewL' if s % 2 else 'skewS', 0.7),
          'sloppy then one el.': lambda s: ('sloppy', s, 'thenL' if s % 2 else 'thenS', 0), 'sloppy one el.': lambda s: ('sloppy', s, 'L' if s % 2 else 'S', 0)}


def agg(rs):
    lv = sorted(r['lives'] for r in rs); n = len(rs); return f"{sum(r['out'] == 'Won' for r in rs):2d}/{n} m{lv[n // 2]:<2d}"


def capacity(a):
    """The largest health multiplier on waves 4-10 (exact health; of the 4e waves, or of `hs`) this run still wins, by bisection on [lo, hi]."""
    crowd, s, perks, pk = a[:4]; hs = a[4] if len(a) > 4 else [h for _, h in pr.MIX]; lo, hi = (0.8, 2.4) if len(a) <= 4 else (0.5, 1.6)
    def wins(m):
        base = pr.params(mix=[(pt, round(h * m)) for (pt, _), h in zip(pr.MIX, hs)]); p = base if perks is None else with_perks(base, perks, s)
        return job((p,) + CROWDS[crowd](s) + (pk, s))['out'] == 'Won'
    if not wins(lo): return lo
    while hi - lo > 0.01:
        mid = (lo + hi) / 2
        if wins(mid): lo = mid
        else: hi = mid
    return lo


def caps(pool_, perks, pk, n=20, crowds=('sloppy reads', 'sloppy half')):
    rs = pool_.map(capacity, [(c, s, perks, pk) for c in crowds for s in range(n)])
    return [statistics.mean(rs[i * n:(i + 1) * n]) for i in range(len(crowds))]


CANDS = [('damage x1.25', perk(DMG, MUL, 1.25, 'L'), 'Light'), ('damage x1.25', perk(DMG, MUL, 1.25, 'S'), 'Shadow'),
         ('fire rate x1.15', perk(SPS, MUL, 1.15), ''), ('fire rate x1.10', perk(SPS, MUL, 1.1), ''), ('range +0.5', perk(RANGE, ADD, 0.5), ''),
         ('range +1', perk(RANGE, ADD, 1.0), ''), ('gold +2', perk(GOLD, ADD, 2), ''), ('gold +3', perk(GOLD, ADD, 3), '')]


def route_in_range(route, cell, r):
    """Length of the route (sampled every 1/8 cell) within r of the cell, and the sections it reaches (left of x=5, between, right of x=10)."""
    n = 0.0; sec = set()
    for a, b in zip(route, route[1:]):
        L = step_len(a, b); k = int(round(L * 8))
        for i in range(k):
            t = (i + 0.5) / k; x = a[0] + (b[0] - a[0]) * t; y = a[1] + (b[1] - a[1]) * t
            if (x - cell[0]) ** 2 + (y - cell[1]) ** 2 <= r * r: n += L / k; sec.add(0 if x < 5 else 1 if x < 10 else 2)
    return n, sec


if __name__ == '__main__':
    pool_ = Pool(17); mode = sys.argv[1]
    if mode == 'worth':
        print('no perks', ['%.3f' % x for x in caps(pool_, None, 'first')], flush=True)
        for lvl in (2, 4, 6, 8):
            for name, m, el in CANDS:
                c = caps(pool_, [m, NOP, NOP], ('at', (lvl,)))
                print(f'level {lvl} {name} {el}: reads {c[0]:.3f} half {c[1]:.3f}', flush=True)
    if mode == 'stack':
        aimed = lambda d: [perk(DMG, MUL, d, 'L'), perk(DMG, MUL, d, 'S'), NOP]
        cands = [('damage x1.25, aimed', aimed(1.25), 'weaker'), ('damage x1.2, aimed', aimed(1.2), 'weaker')] + \
                [(n, [m] * 3, 'first') for n, m, el in CANDS[2:]]
        print('no perks', ['%.3f' % x for x in caps(pool_, None, 'first')], flush=True)
        for name, perks, pk in cands:
            c = caps(pool_, perks, pk); print(f'7 x {name}: reads {c[0]:.3f} half {c[1]:.3f}', flush=True)
    if mode == 'range':
        for amt in (1.0, 0.5):
            prev = None
            for k in range(0, 8):
                c = caps(pool_, [perk(RANGE, ADD, amt), NOP, NOP], ('at', tuple(range(2, 2 + k))))
                v = statistics.mean(c)
                print(f'range +{amt:g} x{k} (levels 2..{1 + k}): reads {c[0]:.3f} half {c[1]:.3f}' + (f'  this pick adds {v - prev:+.3f}' if prev else ''), flush=True)
                prev = v
    if mode == 'disc':
        fl = Field(set(WALLS)); route = fl.route(); total = sum(step_len(a, b) for a, b in zip(route, route[1:]))
        free = [c for c in FREE_CELLS if c not in route]
        print(f'unbuilt route: {total:.2f} cells long; {len(free)} free cells off it')
        for r in (2.5, 3.0, 3.5, 4.5, 5.5, 6.5, 7.5, 8.5, 9.5):
            cov = sorted((route_in_range(route, c, r) for c in free), key=lambda x: -x[0])
            best = cov[0][0]; med = statistics.median(x[0] for x in cov); three = sum(len(x[1]) == 3 for x in cov)
            half = sum(x[0] >= total / 2 for x in cov); disc = math.pi * r * r
            print(f'range {r}: disc {disc:.0f} cells = {100 * disc / 144:.0f}% of the 16x9 board | route in range: best cell {best:.1f} ({100 * best / total:.0f}%), median cell {med:.1f} '
                  f'({100 * med / total:.0f}%) | cells reaching half the route {half}/{len(free)} | cells reaching all three wall sections {three}/{len(free)}')
    if mode == 'margin':
        am = os.environ.get('POOL'); perks = pool(*map(float, am.split(',')[:3]), int(am.split(',')[3])) if am else p5.POOL
        tests = [('reads/reads', 'sloppy reads', 'reads'), ('half/reads', 'sloppy half', 'reads'), ('reads/random', 'sloppy reads', 'random'),
                 ('half/random', 'sloppy half', 'random'), ('reads/gold', 'sloppy reads', 'gold'), ('half/majority', 'sloppy half', 'majority')]
        for a in sys.argv[2:]:
            hs = list(map(int, a.split(','))); n = 20
            rs = pool_.map(capacity, [(c, s, perks, pk, hs) for _, c, pk in tests for s in range(n)])
            out = []
            for i, (name, _, _) in enumerate(tests):
                x = sorted(rs[i * n:(i + 1) * n]); out.append(f'{name} mean {statistics.mean(x):.3f} median {x[n // 2]:.3f} below 1: {sum(v < 1 for v in x)}/{n}')
            print(hs, '|', ' | '.join(out), flush=True)
    if mode == 'retune':
        cands = [list(map(int, a.split(','))) for a in sys.argv[2:]]
        tests = [('reads/reads', 'sloppy reads', 'reads'), ('half/reads', 'sloppy half', 'reads'), ('reads/random', 'sloppy reads', 'random'),
                 ('half/random', 'sloppy half', 'random'), ('reads/gold', 'sloppy reads', 'gold'), ('half/majority', 'sloppy half', 'majority'),
                 ('reads/range', 'sloppy reads', 'range'), ('rand el./random', 'sloppy random el.', 'random'), ('60/40 reads', 'sloppy 60/40', 'reads'),
                 ('then-one/reads', 'sloppy then one el.', 'reads'), ('one/reads', 'sloppy one el.', 'reads')]
        for hs in cands:
            am = os.environ.get('POOL'); perks = pool(*map(float, am.split(',')[:3]), int(am.split(',')[3])) if am else None
            p = p5.with_health(hs, perks=perks); out = []
            for name, c, pk in tests:
                rs = pool_.map(job, [(p,) + CROWDS[c](s) + (pk, s) for s in range(N)]); out.append(f'{name} {agg(rs)}')
            print(hs, '|', ' | '.join(out), flush=True)
