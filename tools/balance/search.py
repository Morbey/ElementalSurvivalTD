# Margin search: health = 100 at wave 6, geometric ratio re below it and rl above it, rounded to 5.
# Scores sloppy humans (20 seeds) at the baseline, at gold per kill -10% and -20%, and at +20% health in waves 1-3.
import sys, json, itertools
from multiprocessing import Pool
from econ import *
def curve(re, rl): return [int(round(100 * (re ** (w - 6) if w < 6 else rl ** (w - 6)) / 5)) * 5 for w in range(1, 11)]
def params(hp, S=100, C=50, g=10, early=1.0, **kw):
    hp = [int(round(h * early / 5)) * 5 if i < 3 else h for i, h in enumerate(hp)]
    p = dict(P, start_gold=S, cost=C, waves=[(c, 1, 15 if i == 0 else 8, hp[i], g) for i, c in enumerate(range(3, 13))]); p.update(kw); return p
def sloppy(p, seeds=range(20)):
    rs = [run(p, Sloppy(s)) for s in seeds]; lv = sorted(r['lives'] for r in rs)
    return (sum(r['out'] == 'Won' for r in rs), lv[len(lv) // 2])
def job(a):
    S, C, g, re, rl = a; hp = curve(re, rl); out = {}
    for name, kw in (('base', {}), ('g-10', {'g': g * 0.9}), ('g-20', {'g': g * 0.8}), ('early+20', {'early': 1.2})):
        q = dict(S=S, C=C, g=g); q.update(kw); q['g'] = int(round(q['g']))
        p = params(hp, **q); out[name] = sloppy(p)
        if name == 'base': r = run(p, Sensible()); out['sensible'] = (r['out'], r['lives'])
    return a, hp, out
if __name__ == '__main__':
    grid = json.loads(sys.argv[1])
    for a, hp, out in Pool(8).imap(job, list(itertools.product(*grid))):
        print(a, hp, out, flush=True)
