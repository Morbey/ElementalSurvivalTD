# Offline maze order on an empty board: opens like the sensible bot (2 towers), then each tower by two-step lookahead
# on total path in range, route changes allowed. Writes strong_order.json.
import json, sys, time
from multiprocessing import Pool
from econ import *
def score(blocked, towers):
    fl = Field(blocked)
    if fl.dist(SPAWN) == UNREACH: return None
    r = fl.route(); return (round(exposure(r, towers), 3), fl.dist(SPAWN))
def second(args):
    blocked, towers, c, s1 = args
    blocked = set(blocked) | {c}; best = s1
    for d in FREE_CELLS:
        if d in blocked: continue
        blocked.add(d); s = score(blocked, towers + [c, d]); blocked.discard(d)
        if s and s > best: best = s
    return (best, s1, c)
if __name__ == '__main__':
    n = int(sys.argv[1]); opening = [(2, 5), (3, 5)] if len(sys.argv) < 3 else json.loads(sys.argv[2])
    blocked = set(WALLS) | {tuple(c) for c in opening}; towers = [tuple(c) for c in opening]; t0 = time.time()
    pool = Pool(8)
    for k in range(len(towers), n):
        first = []
        for c in FREE_CELLS:
            if c in blocked: continue
            blocked.add(c); s = score(blocked, towers + [c]); blocked.discard(c)
            if s: first.append((s, c))
        first.sort(key=lambda x: (-x[0][0], -x[0][1], x[1]))
        res = pool.map(second, [(frozenset(blocked), towers, c, s1) for s1, c in first[:40]])
        best = max(res, key=lambda r: (r[0], r[1], (-r[2][0], -r[2][1])))
        c = best[2]; blocked.add(c); towers.append(c)
        print(k + 1, c, score(blocked, towers), round(time.time() - t0), flush=True)
    json.dump(towers, open('strong_order.json', 'w'))
