# core_old.py with the 3c movement: 8 directions, costs 408/577, no corner cutting, step length, 4-cell occupancy.
import core_old as o
from core_old import *
from field8 import DIRS, OFFS

S, D = 408, 577
DIAG = f(577.0 / 408.0)

class Field(o.Field):
    def free(self, x, y): return 0 <= x < W and 0 <= y < H and (x, y) not in self.blocked
    def can_step(self, x, y, d):
        dx, dy = DIRS[d]
        if not self.free(x + dx, y + dy): return False
        return self.free(x + dx, y) and self.free(x, y + dy) if dx and dy else True
    def compute(self):
        d = {}
        if EXIT not in self.blocked:
            d[EXIT] = 0; q = deque([EXIT]); inq = {EXIT}
            while q:
                c = q.popleft(); inq.discard(c); x, y = c
                for k in range(8):
                    if not self.can_step(x, y, k): continue
                    dx, dy = DIRS[k]; nb = (x + dx, y + dy); nd = d[c] + (D if dx and dy else S)
                    if nd < d.get(nb, UNREACH):
                        d[nb] = nd
                        if nb not in inq: inq.add(nb); q.append(nb)
        self.d = d
    def next(self, frm, came):
        cur = self.dist(frm)
        if cur == 0 or cur == UNREACH: return None
        hv = (frm[0] - came[0], frm[1] - came[1]); h = DIRS.index(hv) if hv != (0, 0) else 0
        for off in OFFS:
            k = (h + off) & 7
            if not self.can_step(frm[0], frm[1], k): continue
            dx, dy = DIRS[k]; n = (frm[0] + dx, frm[1] + dy)
            if self.dist(n) + (D if dx and dy else S) == cur: return n
        return None

_OldEnemy = o.Enemy
class Enemy(_OldEnemy):
    __slots__ = ('len',)
    def __init__(self, field, speed, hp, bounty):
        self.len = 1.0
        _OldEnemy.__init__(self, field, speed, hp, bounty); self.setlen(); self.sync()
    def setlen(self):
        self.len = DIAG if (self.to[0] != self.frm[0] and self.to[1] != self.frm[1]) else 1.0
    def sync(self):
        t = f(self.p / self.len)
        self.x = f(self.frm[0] + f((self.to[0] - self.frm[0]) * t))
        self.y = f(self.frm[1] + f((self.to[1] - self.frm[1]) * t))
    def dte(self):
        return 0.0 if self.arrived else f(f(self.field.dist(self.to) / f(408.0)) + f(self.len - self.p))
    def step(self):
        if not (self.arrived or self.frm == self.to):
            adv = f(DT * self.speed)
            if adv > 0:
                self.p = f(self.p + adv)
                while self.p >= self.len:
                    self.p = f(self.p - self.len); left = self.frm; self.frm = self.to
                    n = self.field.next(self.frm, left)
                    if n is not None: self.to = n; self.setlen()
                    else:
                        self.to = self.frm; self.p = 0.0; self.len = 1.0; self.arrived = self.field.dist(self.frm) == 0; break
        self.sync()
        return self.arrived

class Battle(o.Battle):
    def __init__(self, *a, **k):
        o.Battle.__init__(self, *a, **k); self.field = Field(self.blocked)
    def can_place(self, c):
        if self.gold < self.cost: return 'NotEnoughGold'
        if not (0 <= c[0] < W and 0 <= c[1] < H): return 'OutOfBounds'
        if c == SPAWN or c == EXIT: return 'SpawnOrExit'
        if c in self.blocked: return 'CellBlocked'
        for e in self.enemies:
            if min(e.frm[0], e.to[0]) <= c[0] <= max(e.frm[0], e.to[0]) and min(e.frm[1], e.to[1]) <= c[1] <= max(e.frm[1], e.to[1]):
                return 'EnemyInTheWay'
        self.blocked.add(c); self.field.compute()
        ok = self.field.dist(SPAWN) != UNREACH and all(self.field.dist(e.to) != UNREACH for e in self.enemies)
        self.blocked.discard(c); self.field.compute()
        return 'Placed' if ok else 'WouldBlockPath'

# Battle.tick creates o.Enemy by global name inside core_old; rebind it there.
o.Enemy = Enemy
