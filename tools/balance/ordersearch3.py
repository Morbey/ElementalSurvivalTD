# All 89 orders on the proposal's health and bonus: reading vs half-and-half for sensible, maze and sloppy cells, and random.
import sys, itertools
from multiprocessing import Pool
from p4 import *
from proposal4b import params
from grid2 import agg
from ordersearch import ok
NS = 12
def job(o):
    p = params(order=o); out = {}
    for name, kind, e in (('sR', 'sensible', 'board'), ('mR', 'maze', 'board'), ('sHL', 'sensible', 'halfL'), ('sHS', 'sensible', 'halfS'),
                          ('mHL', 'maze', 'halfL'), ('mHS', 'maze', 'halfS')):
        out[name] = short(one((p, kind, 0, e, 0)))
    out['sl'] = agg([one((p, 'sloppy', s, 'board', 0)) for s in range(NS)])
    out['slhalf'] = agg([one((p, 'sloppy', s, 'halfL' if s % 2 else 'halfS', 0)) for s in range(NS)])
    out['rand'] = agg([one((p, 'sensible', 0, 'random', s)) for s in range(NS)])
    return o, out
if __name__ == '__main__':
    orders = [''.join(o) for o in itertools.product('LS', repeat=10) if ok(o)]
    for o, out in Pool(17).imap_unordered(job, orders):
        print(o, ' '.join(f'{k}={v}' for k, v in out.items()), flush=True)
