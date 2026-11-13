# Step 4e design search: why these groups and this health (docs/balance/0001, "How the waves were chosen").
# python3 explore4e.py patterns   every pattern family at the proposal's health
# python3 explore4e.py health     health candidates of waves 4 to 10 on the proposal's groups, at 0 and +1 hit
# python3 explore4e.py margin     per wave, the proposal with only that wave's health scaled
import sys
from multiprocessing import Pool
from p4 import *
import proposal as pr

HEALTH = [h for _, h in pr.MIX]
PATTERNS = {  # waves 4 to 10, one letter per enemy in entry order
    'proposal: blocks, then streams, uneven 7 and 8': [p for p, _ in pr.MIX],
    'blocks 4-5, stream 6, Shadow only 7, streams 8-10': ['SSSLLL', 'LLLLSSS', 'SLSLSLSL', 'SSSSSSSSS', 'LSLSLSLSLS', 'SLSLSLSLSLS', 'LSLSLSLSLSLS'],
    'streams 4-10': ['SLSLSL', 'LSLSLSL', 'SLSLSLSL', 'SLSLSLSLS', 'LSLSLSLSLS', 'SLSLSLSLSLS', 'LSLSLSLSLSLS'],
    'two blocks 4-10': ['SSSLLL', 'LLLLSSS', 'SSSSLLLL', 'LLLLSSSSS', 'SSSSSLLLLL', 'LLLLLLSSSSS', 'SSSSSSLLLLLL'],
    'pairs': ['SSSLLL', 'LLLLSSS', 'SSLLSSLL', 'SSLSSLSSL', 'LLSLLSLLSL', 'SSLLSSLLSSL', 'LLSSLLSSLLSS'],
}
HEALTHS = [[85, 105, 135, 180, 245, 345, 460], [120, 120, 200, 200, 280, 360, 440], [100, 100, 180, 200, 280, 360, 440],
           [100, 100, 180, 180, 260, 340, 440], [160, 160, 250, 250, 320, 370, 370], HEALTH]
FIXED = [('read', 'sensible', 0, 'board', 0), ('maze', 'maze', 0, 'board', 0), ('L', 'sensible', 0, 'L', 0), ('S', 'sensible', 0, 'S', 0),
         ('saver', 'saver', 3, 'board', 0), ('late', 'late', 2, 'board', 0), ('half L', 'sensible', 0, 'halfL', 0), ('half S', 'sensible', 0, 'halfS', 0)]
CROWDS = [('read', lambda s: ('sloppy', s, 'board', 0)), ('half', lambda s: ('sloppy', s, 'halfL' if s % 2 else 'halfS', 0)),
          ('random', lambda s: ('sensible', 0, 'random', s)), ('60/40', lambda s: ('sloppy', s, 'skewL' if s % 2 else 'skewS', 0.6)),
          ('70/30', lambda s: ('sloppy', s, 'skewL' if s % 2 else 'skewS', 0.7)), ('then one element', lambda s: ('sloppy', s, 'thenL' if s % 2 else 'thenS', 0))]


def agg(rs):
    lv = sorted(r['lives'] for r in rs); n = len(rs); return f"{sum(r['out'] == 'Won' for r in rs)}/{n} m{lv[n // 2]}"


def score(pool, p, n=40):
    js = [(p,) + j[1:] for j in FIXED]
    for _, f in CROWDS: js += [(p,) + f(s) for s in range(n)]
    rs = pool.map(one, js); k = len(FIXED)
    out = ' '.join(f"{nm}:{x['out'][0]}{x['lives']}" for (nm, *_), x in zip(FIXED, rs[:k])) + ' | sloppy:'
    for nm, _ in CROWDS: out += f" {nm} {agg(rs[k:k + n])}"; k += n
    sl = rs[len(FIXED):len(FIXED) + n]
    return out + f" | recoverable {100 * sum(r['recoverable'] for r in sl) / sum(r['shots'] for r in sl):.0f}%"


def mix(pats, hs): return list(zip(pats, hs))


if __name__ == '__main__':
    pool = Pool(17); mode = sys.argv[1] if len(sys.argv) > 1 else 'patterns'; pats = [p for p, _ in pr.MIX]
    if mode == 'patterns':
        for name, ps in PATTERNS.items(): print(name, '->', score(pool, pr.params(mix=mix(ps, HEALTH))), flush=True)
    if mode == 'health':
        for hs in HEALTHS:
            for d in (0, 1):
                print(hs, f'+{d} hit' if d else '', '->', score(pool, pr.params(mix=mix(pats, [h + 20 * d for h in hs]))), flush=True)
    if mode == 'margin':
        for k in range(3, 10):
            row = []
            for m in (1.0, 1.15, 1.3, 1.5):
                p = pr.params(hpmul=lambda i, h, m=m, k=k: h * (m if i == k else 1))
                rs = pool.map(one, [(p, 'sloppy', s, 'board', 0) for s in range(40)])
                row.append(f"x{m}: leaks {sum(r['leaks_w'][k] for r in rs) / 40:.1f}, won {sum(r['out'] == 'Won' for r in rs)}")
            print('wave', k + 1, ' | '.join(row), flush=True)
