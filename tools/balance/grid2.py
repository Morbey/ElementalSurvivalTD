# Bonus x late health search: sloppy readers at the proposal, at gold per kill -20%, at late health +20%;
# plus the reading bots, half-and-half and random at the proposal.
import sys, json, itertools
from multiprocessing import Pool
from p4 import *
from grid import BASE
def hpc(m, m2=1.0):
    """3c health with waves 7-10 times m (a number, or one factor per wave 7-10), then times m2."""
    ms = list(m) if isinstance(m, (list, tuple)) else [m] * 4
    return [int(round(h * (1.0 if i < 6 else ms[i - 6] * m2) / 5)) * 5 for i, h in enumerate(BASE)]
def bon(spec):
    kind, v = spec
    return [v] * 9 + [0] if kind == 'flat' else [v * (k + 1) for k in range(9)] + [0] if kind == 'lin' else [v + 5 * k for k in range(9)] + [0]
NS = 20
def agg(rs): lv = sorted(r['lives'] for r in rs); return f"{sum(r['out'] == 'Won' for r in rs)}/{len(rs)} m{lv[len(lv)//2]}"
def job(a):
    o, m, spec = a; B = bon(spec); out = {}
    p = params4(hpc(m), o, B)
    for name, kind, e in (('sens', 'sensible', 'board'), ('maze', 'maze', 'board'), ('halfL', 'sensible', 'halfL'), ('halfS', 'sensible', 'halfS'), ('monoS', 'sensible', 'S')):
        out[name] = short(one((p, kind, 0, e, 0)))
    out['sl'] = agg([one((p, 'sloppy', s, 'board', 0)) for s in range(NS)])
    out['sl_g8'] = agg([one((params4(hpc(m), o, B, g=8), 'sloppy', s, 'board', 0)) for s in range(NS)])
    out['sl_hp12'] = agg([one((params4(hpc(m, 1.2), o, B), 'sloppy', s, 'board', 0)) for s in range(NS)])
    out['rand'] = agg([one((p, 'sensible', 0, 'random', s)) for s in range(NS)])
    out['slhalf'] = agg([one((p, 'sloppy', s, 'halfL' if s % 2 else 'halfS', 0)) for s in range(NS)])
    return a, out
if __name__ == '__main__':
    orders, ms, specs = (json.loads(x) for x in sys.argv[1:4])
    for a, out in Pool(17).imap(job, list(itertools.product(orders, [tuple(m) if isinstance(m, list) else m for m in ms], [tuple(s) for s in specs]))):
        print(a, ' '.join(f'{k}={v}' for k, v in out.items()), flush=True)
