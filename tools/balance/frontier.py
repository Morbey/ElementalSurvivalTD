# 40-seed sloppy readers on candidate (health, bonus) pairs: proposal, gold per kill -10%/-20%, waves 7-10 health +20%; random and sloppy half.
import sys, json
from multiprocessing import Pool
from p4 import *
from proposal4b import params, up
from grid2 import agg
if __name__ == '__main__':
    pool = Pool(17)
    for hp, bonus in json.loads(sys.argv[1]):
        out = []
        for name, p in (('base', params(hp=hp, bonus=bonus)), ('g9', params(hp=hp, bonus=bonus, g=9)), ('g8', params(hp=hp, bonus=bonus, g=8)),
                        ('hp12', params(hp=up(hp, 1.2), bonus=bonus))):
            out.append(name + ' ' + agg(pool.map(one, [(p, 'sloppy', s, 'board', 0) for s in range(40)])))
        p = params(hp=hp, bonus=bonus)
        out.append('rand ' + agg(pool.map(one, [(p, 'sensible', 0, 'random', s) for s in range(40)])))
        out.append('slhalf ' + agg(pool.map(one, [(p, 'sloppy', s, 'halfL' if s % 2 else 'halfS', 0) for s in range(40)])))
        print(hp, bonus, ' | '.join(out), flush=True)
