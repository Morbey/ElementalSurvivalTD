# Robustness battery for 4b variants (order, health, bonus): reading bots at starting gold 92/115/138 and cost 48; 40 sloppy readers at
# the proposal, gold per kill -20%, late health +20%, starting gold -20%/+20%; random elements and sloppy half-and-half.
import sys, json
from multiprocessing import Pool
from p4 import *
from proposal import up
from grid2 import agg
if __name__ == '__main__':
    pool = Pool(17)
    for o, hp, bon in json.loads(sys.argv[1]):
        P = lambda **k: params4(k.pop('hp', hp), o, bon, **k)
        bots = pool.map(one, [(P(S=s), kd, 0, 'board', 0) for s in (92, 115, 138) for kd in ('sensible', 'maze')] + [(P(C=48), 'sensible', 0, 'board', 0)])
        out = [' '.join(short(r) for r in bots)]
        for name, p in (('base', P()), ('g8', P(g=8)), ('hp12', P(hp=up(hp, 1.2))), ('S92', P(S=92)), ('S138', P(S=138))):
            out.append(name + ' ' + agg(pool.map(one, [(p, 'sloppy', s, 'board', 0) for s in range(40)])))
        out.append('rand ' + agg(pool.map(one, [(P(), 'sensible', 0, 'random', s) for s in range(40)])))
        out.append('slhalf ' + agg(pool.map(one, [(P(), 'sloppy', s, 'halfL' if s % 2 else 'halfS', 0) for s in range(40)])))
        print(o, hp, bon, '|', ' | '.join(out), flush=True)
