# Every element order of 10 waves with runs of at most 2: reading builds vs half-and-half, on a given health curve and bonus.
import sys, json, itertools
from multiprocessing import Pool
from p4 import *
def ok(o): return all(not (o[i] == o[i+1] == o[i+2]) for i in range(8)) and o[0] == 'S'  # S first: the mirror is symmetric
def job(a):
    o, hp, bonus = a; p = params4(hp, o, bonus); out = {}
    for name, kind, arg, e in (('sens', 'sensible', 0, 'board'), ('sensR', 'sensible', 0, 'reads'), ('maze', 'maze', 0, 'board'),
                               ('halfL', 'sensible', 0, 'halfL'), ('halfS', 'sensible', 0, 'halfS')):
        r = one((p, kind, arg, e, 0)); out[name] = (r['out'][0], r['lives'])
    sl = [one((p, 'sloppy', s, 'board', 0)) for s in range(8)]
    out['sloppy'] = (sum(r['out'] == 'Won' for r in sl), sorted(r['lives'] for r in sl)[4])
    return o, out
if __name__ == '__main__':
    hp = json.loads(sys.argv[1]); bonus = json.loads(sys.argv[2])
    orders = [''.join(o) for o in itertools.product('LS', repeat=10) if ok(o)]
    print(len(orders), 'orders', flush=True)
    for o, out in Pool(17).imap_unordered(job, [(o, hp, bonus) for o in orders]):
        print(o, json.dumps(out), flush=True)
