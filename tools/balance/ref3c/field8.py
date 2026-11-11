# Prototype of the 3c flow field: 8 neighbours, whole-number costs, no corner cutting, label-correcting queue.
import random, heapq, math
from decimal import Decimal, getcontext
getcontext().prec = 60
SQRT2 = Decimal(2).sqrt()

# compass order, counter-clockwise from +x
DIRS = [(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1),(0,-1),(1,-1)]
OFFS = [0,1,-1,2,-2,3,-3,4]
UNREACH = 2**31-1

class Field:
    def __init__(self, w, h, blocked, exit_, straight=408, diagonal=577):
        self.w, self.h, self.blocked, self.exit = w, h, blocked, exit_
        self.S, self.D = straight, diagonal
        self.compute()

    def free(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h and (x, y) not in self.blocked

    def can_step(self, x, y, d):
        dx, dy = DIRS[d]
        if not self.free(x+dx, y+dy): return False
        if dx != 0 and dy != 0:
            return self.free(x+dx, y) and self.free(x, y+dy)
        return True

    def compute(self):
        w, h = self.w, self.h
        dist = [UNREACH]*(w*h); queued = [False]*(w*h)
        self.pops = 0; self.relax = 0
        if self.exit not in self.blocked:
            q = [0]*(w*h); head = 0; count = 0
            ei = self.exit[1]*w + self.exit[0]
            dist[ei] = 0; q[0] = ei; count = 1; queued[ei] = True
            n = w*h
            while count > 0:
                c = q[head]; head = (head+1) % n; count -= 1; queued[c] = False; self.pops += 1
                x, y = c % w, c // w
                for d in range(8):
                    self.relax += 1
                    if not self.can_step(x, y, d): continue
                    dx, dy = DIRS[d]
                    nd = dist[c] + (self.D if dx and dy else self.S)
                    ni = (y+dy)*w + x+dx
                    if nd < dist[ni]:
                        dist[ni] = nd
                        if not queued[ni]:
                            queued[ni] = True; q[(head+count) % n] = ni; count += 1
        self.d = dist

    def dist(self, c):
        return self.d[c[1]*self.w + c[0]]

    def next(self, frm, came):
        cur = self.dist(frm)
        if cur == 0 or cur == UNREACH: return None
        hx, hy = frm[0]-came[0], frm[1]-came[1]
        h = DIRS.index((hx, hy)) if (hx, hy) != (0, 0) else 0
        for o in OFFS:
            d = (h + o) & 7
            if not self.can_step(frm[0], frm[1], d): continue
            dx, dy = DIRS[d]
            n = (frm[0]+dx, frm[1]+dy)
            if self.dist(n) + (self.D if dx and dy else self.S) == cur: return n
        return None

    def route(self, spawn):
        cells = [spawn]; frm = spawn; came = spawn
        while True:
            n = self.next(frm, came)
            if n is None: return cells
            cells.append(n); came, frm = frm, n

def exact(w, h, blocked, exit_):
    """Dijkstra on exact (straight, diagonal) counts compared with 60-digit decimals."""
    f = Field(w, h, blocked, exit_)
    best = {}
    if exit_ in blocked: return best
    pq = [(Decimal(0), 0, 0, exit_)]
    while pq:
        cst, a, b, c = heapq.heappop(pq)
        if c in best: continue
        best[c] = (a, b)
        for d in range(8):
            if not f.can_step(c[0], c[1], d): continue
            dx, dy = DIRS[d]
            n = (c[0]+dx, c[1]+dy)
            if n in best: continue
            na, nb = (a, b+1) if dx and dy else (a+1, b)
            heapq.heappush(pq, (na + nb*SQRT2, na, nb, n))
    return best

W, H = 16, 9
SPAWN, EXIT = (0, 4), (15, 4)
WALLS = frozenset({(5, y) for y in range(0, 7)} | {(10, y) for y in range(2, 9)})

if __name__ == '__main__':
    f = Field(W, H, set(WALLS), EXIT)
    r = f.route(SPAWN)
    print('prototype dist', f.dist(SPAWN), 'cells', f.dist(SPAWN)/408, 'steps', len(r)-1)
    print('route', r)
    s = sum(1 for a, b in zip(r, r[1:]) if a[0] == b[0] or a[1] == b[1]); print('straight', s, 'diag', len(r)-1-s)
    print('pops', f.pops, 'neighbour checks', f.relax)
    f = Field(W, H, set(), EXIT); print('empty: pops', f.pops, 'checks', f.relax, 'dist', f.dist(SPAWN))

    # exactness of 408/577 and of 10/14 against true sqrt(2) on random boards
    rnd = random.Random(7)
    worst_pops = 0; tot_pops = 0; n_boards = 0
    bad = {(408, 577): 0, (10, 14): 0, (5, 7): 0, (70, 99): 0}
    examples = {}
    for trial in range(3000):
        blocked = set(WALLS) if trial % 2 else set()
        for _ in range(rnd.randint(0, 60)):
            c = (rnd.randrange(W), rnd.randrange(H))
            if c not in (SPAWN, EXIT): blocked.add(c)
        ex = exact(W, H, blocked, EXIT)
        for (S, D) in bad:
            g = Field(W, H, blocked, EXIT, S, D)
            if (S, D) == (408, 577):
                worst_pops = max(worst_pops, g.pops); tot_pops += g.pops; n_boards += 1
            ok = True
            for y in range(H):
                for x in range(W):
                    c = (x, y)
                    if c in ex:
                        a, b = ex[c]
                        if g.dist(c) != a*S + b*D: ok = False
                    elif g.dist(c) != UNREACH: ok = False
            # the integer field must also pick only truly shortest steps: every step it offers lies on a true shortest path
            if ok:
                for y in range(H):
                    for x in range(W):
                        c = (x, y)
                        if c not in ex or c == EXIT: continue
                        for d in range(8):
                            if not g.can_step(x, y, d): continue
                            dx, dy = DIRS[d]; n = (x+dx, y+dy)
                            st = D if dx and dy else S
                            on_int = g.dist(n) + st == g.dist(c)
                            a, b = ex[n]; a2, b2 = ex[c]
                            tn = a + b*SQRT2 + (SQRT2 if dx and dy else 1)
                            on_true = abs(tn - (a2 + b2*SQRT2)) < Decimal('1e-40')
                            if on_int != on_true: ok = False
            if not ok:
                bad[(S, D)] += 1; examples.setdefault((S, D), sorted(blocked - WALLS)[:12])
    print('boards', n_boards, 'boards where the whole-number field differs from true sqrt2:', bad)
    print('408/577 pops: mean', tot_pops/n_boards, 'worst', worst_pops, '(cells 144)')
