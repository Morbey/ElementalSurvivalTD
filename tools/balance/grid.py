# Quick grid: orders x late-health multipliers x bonus x gold per kill -> reading vs not reading.
import sys, json, itertools
from multiprocessing import Pool
from p4 import *
BASE = [45, 55, 65, 75, 90, 100, 130, 175, 230, 305]
def hpcurve(m, early=1.0): return [int(round(h * (early if i < 6 else m) / 5)) * 5 for i, h in enumerate(BASE)]
NS = 12
def job(a):
    o, m, bonus, g = a; p = params4(hpcurve(m), o, bonus, g=g); out = {}
    for name, kind, e in (('sens', 'sensible', 'board'), ('maze', 'maze', 'board'), ('halfL', 'sensible', 'halfL'), ('halfS', 'sensible', 'halfS')):
        r = one((p, kind, 0, e, 0)); out[name] = short(r)
    sl = [one((p, 'sloppy', s, 'board', 0)) for s in range(NS)]; lv = sorted(r['lives'] for r in sl)
    out['sloppy'] = f"{sum(r['out'] == 'Won' for r in sl)}/{NS} m{lv[NS//2]}"
    rd = [one((p, 'sensible', 0, 'random', s)) for s in range(NS)]; lv = sorted(r['lives'] for r in rd)
    out['random'] = f"{sum(r['out'] == 'Won' for r in rd)}/{NS} m{lv[NS//2]}"
    hs = [one((p, 'sloppy', s, 'halfL' if s % 2 else 'halfS', 0)) for s in range(NS)]; lv = sorted(r['lives'] for r in hs)
    out['sloppyhalf'] = f"{sum(r['out'] == 'Won' for r in hs)}/{NS} m{lv[NS//2]}"
    return a, out
if __name__ == '__main__':
    orders, ms, bonuses, gs = (json.loads(x) for x in sys.argv[1:5])
    for a, out in Pool(17).imap(job, list(itertools.product(orders, ms, bonuses, gs))):
        print(a, ' '.join(f'{k}={v}' for k, v in out.items()), flush=True)
