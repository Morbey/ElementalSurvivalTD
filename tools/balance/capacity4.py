# Largest health a fixed build of n counter towers (Light, x2) kills with no leak in one Shadow wave of 10 at 1/s.
from multiprocessing import Pool
from econ import *
def sensible_order(n):
    b = Battle(mk_waves(P), 10, TS, gold=10**6, cost=0); s = Sensible(); out = []
    for _ in range(n): c = s.pick(b)[0]; b.place(c); out.append(c)
    return out
def leaks(layout, hp):
    b = Battle([((SPEED, hp, 0, 'S'), 10, 1, 0.5)], 100, TS)
    for x in layout: b.place(x, 'L')
    while b.outcome == 'Running': b.tick()
    return b.leaks
def cap(layout):
    lo, hi = 0, 8000
    while hi - lo > 5:
        m = (lo + hi) // 2
        if leaks(layout, m) == 0: lo = m
        else: hi = m
    return lo
if __name__ == '__main__':
    SO = sensible_order(24); MO = strong_order(); pool = Pool(17); ns = (4, 6, 8, 10, 12, 16, 20)
    for name, order in (('sensible', SO), ('maze', MO)): print(name, dict(zip(ns, pool.map(cap, [order[:n] for n in ns]))), flush=True)
