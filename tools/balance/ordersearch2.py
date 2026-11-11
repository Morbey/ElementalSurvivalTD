# All 89 orders (S first, runs <= 2) on one health curve and bonus, with the grid2 metrics.
import sys, json, itertools
from multiprocessing import Pool
import grid2
from grid2 import *
from ordersearch import ok
if __name__ == '__main__':
    grid2.NS = int(sys.argv[3]); m = float(sys.argv[1]); spec = tuple(json.loads(sys.argv[2]))
    orders = [''.join(o) for o in itertools.product('LS', repeat=10) if ok(o)]
    for a, out in Pool(17).imap_unordered(job, [(o, m, spec) for o in orders]):
        print(a[0], ' '.join(f'{k}={v}' for k, v in out.items()), flush=True)
