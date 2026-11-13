# Step 4a (bonus only, neutral towers and enemies): health curves x bonus lists, sloppy humans at the proposal,
# gold per kill -10% / -20% and waves 7-10 health +20%.
import sys, json
from multiprocessing import Pool
from p4 import *
from proposal4b import up
from grid2 import agg
N = [None] * 10
NS = int(sys.argv[2]) if len(sys.argv) > 2 else 20
def job(a):
    hp, bonus = a; p = params4(hp, N, bonus); out = {}
    for name, kind in (('sens', 'sensible'), ('maze', 'maze')): out[name] = short(one((p, kind, 0, 'none', 0)))
    for name, q in (('sl', p), ('g9', params4(hp, N, bonus, g=9)), ('g8', params4(hp, N, bonus, g=8)), ('hp12', params4(up(hp, 1.2), N, bonus))):
        out[name] = agg([one((q, 'sloppy', s, 'none', 0)) for s in range(NS)])
    return a, out
if __name__ == '__main__':
    cases = [(tuple(h), tuple(b)) for h, b in json.loads(sys.argv[1])]
    for a, out in Pool(17).imap(job, cases): print(list(a[0]), list(a[1]), ' '.join(f'{k}={v}' for k, v in out.items()), flush=True)
