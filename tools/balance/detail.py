import sys, json
from multiprocessing import Pool
from p4 import *
from grid2 import hpc, bon
def show(r): return f"{short(r)} L{r['nL']}/S{r['nS']} leaks/wave {r['leaks_w']} waste {r['wasted']}/{r['shots']} rec {r['recoverable']} bonus {r['bonus']} kills {r['earned']}"
if __name__ == '__main__':
    o, m, spec = sys.argv[1], json.loads(sys.argv[2]), json.loads(sys.argv[3]); g = int(sys.argv[4]) if len(sys.argv) > 4 else 10
    m2 = float(sys.argv[5]) if len(sys.argv) > 5 else 1.0
    hp = m if isinstance(m, list) and len(m) == 10 else hpc(m, m2)
    p = params4(hp, o, bon(spec), g=g); print(hp, bon(spec))
    jobs = [(p, 'sensible', 0, 'board', 0), (p, 'maze', 0, 'board', 0), (p, 'sensible', 0, 'halfL', 0), (p, 'sensible', 0, 'halfS', 0)] + [(p, 'sloppy', s, 'board', 0) for s in range(6)] + [(p, 'sensible', 0, 'random', s) for s in range(6)]
    for j, r in zip(jobs, Pool(16).map(one, jobs)): print(j[1], j[3], j[4], show(r))
