# Float32-faithful port of ElementalTD.Core (Battle, Board, WaveSchedule, Enemy, PathFollower, FlowField, Projectile)
# as of commit aadb638 + slice 3a, plus the proposed 3b economy (gold, cost, bounty, sell) as an optional layer.
import struct, math
from collections import deque

_S = struct.Struct('f')
def f(x):  # round to IEEE float32, as C# float arithmetic does
    return _S.unpack(_S.pack(x))[0]

DT = f(1.0 / 60.0)
W, H = 16, 9
SPAWN = (0, 4); EXIT = (15, 4)
WALLS = frozenset({(5, y) for y in range(0, 7)} | {(10, y) for y in range(2, 9)})
UNREACH = 2**31 - 1


class Field:
    def __init__(self, blocked):
        self.blocked = blocked; self.compute()

    def compute(self):
        d = {}
        if EXIT not in self.blocked:
            d[EXIT] = 0; q = deque([EXIT])
            while q:
                c = q.popleft(); n = d[c] + 1; x, y = c
                for nb in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nb[0] < W and 0 <= nb[1] < H and nb not in d and nb not in self.blocked:
                        d[nb] = n; q.append(nb)
        self.d = d

    def dist(self, c): return self.d.get(c, UNREACH)

    def next(self, frm, came):
        cur = self.dist(frm)
        if cur == 0 or cur == UNREACH: return None
        x, y = frm
        nbs = ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
        best = cur
        for n in nbs:
            if 0 <= n[0] < W and 0 <= n[1] < H:
                dn = self.dist(n)
                if dn < best: best = dn
        if best >= cur: return None
        ahead = (2 * x - came[0], 2 * y - came[1])
        if ahead != frm and 0 <= ahead[0] < W and 0 <= ahead[1] < H and self.dist(ahead) == best: return ahead
        back = False
        for n in nbs:
            if not (0 <= n[0] < W and 0 <= n[1] < H) or self.dist(n) != best: continue
            if n == came: back = True; continue
            return n
        return came if back else None

    def route(self):
        """Cells an enemy entering now walks, spawn to exit."""
        cells = [SPAWN]; frm = SPAWN; came = SPAWN
        while True:
            n = self.next(frm, came)
            if n is None: return cells
            cells.append(n); came, frm = frm, n


class Enemy:
    __slots__ = ('field', 'speed', 'hp', 'maxhp', 'frm', 'to', 'p', 'arrived', 'x', 'y', 'on', 'bounty')

    def __init__(self, field, speed, hp, bounty):
        self.field = field; self.speed = f(speed); self.hp = f(hp); self.maxhp = self.hp; self.bounty = bounty
        self.frm = SPAWN; n = field.next(SPAWN, SPAWN); self.to = n if n else SPAWN
        self.p = 0.0; self.arrived = self.frm == self.to and field.dist(SPAWN) == 0; self.on = True; self.sync()

    def sync(self):
        t = self.p
        self.x = f(self.frm[0] + f((self.to[0] - self.frm[0]) * t))
        self.y = f(self.frm[1] + f((self.to[1] - self.frm[1]) * t))

    def dte(self):
        return 0.0 if self.arrived else f(self.field.dist(self.to) + f(1.0 - self.p))

    def step(self):
        if not (self.arrived or self.frm == self.to):
            adv = f(DT * self.speed)
            if adv > 0:
                self.p = f(self.p + adv)
                while self.p >= 1.0:
                    self.p = f(self.p - 1.0); left = self.frm; self.frm = self.to
                    n = self.field.next(self.frm, left)
                    if n is not None: self.to = n
                    else:
                        self.to = self.frm; self.p = 0.0; self.arrived = self.field.dist(self.frm) == 0; break
        self.sync()
        return self.arrived


class Tower:
    __slots__ = ('cell', 'rng2', 'dmg', 'cd_add', 'ps', 'cd', 'fired', 'cost')

    def __init__(self, cell, ts, cost):
        self.cell = cell; r = f(ts['range']); self.rng2 = f(r * r); self.dmg = f(ts['damage'])
        self.cd_add = f(1.0 / f(ts['sps'])); self.ps = f(ts['pspeed']); self.cd = 0.0; self.fired = False; self.cost = cost


class Waves:
    def __init__(self, waves):  # waves: list of (enemy(speed,hp,bounty), count, interval, delay)
        self.w = waves; self.i = 0; self.n = 0; self.timer = f(waves[0][3]); self.done = False; self.started = 0

    def step(self):
        if self.done: return None
        self.timer = f(self.timer - DT)
        if self.timer > 0: return None
        if self.timer < -DT: self.timer = -DT
        wave = self.w[self.i]; e = wave[0]
        if self.n == 0: self.started += 1
        self.n += 1
        if self.n < wave[1]:
            self.timer = f(self.timer + f(wave[2])); return e
        self.i += 1; self.n = 0
        if self.i < len(self.w): self.timer = f(self.timer + f(self.w[self.i][3]))
        else: self.done = True
        return e


class Battle:
    def __init__(self, waves, lives, tower_stats, gold=0, cost=0, refund_pct=100):
        self.blocked = set(WALLS); self.field = Field(self.blocked)
        self.towers = []; self.enemies = []; self.proj = []
        self.waves = Waves(waves); self.lives = lives; self.outcome = 'Running'; self.tick_n = 0
        self.ts = tower_stats; self.gold = gold; self.cost = cost; self.refund_pct = refund_pct
        self.kills = 0; self.leaks = 0; self.earned = 0; self.spent = 0; self.sold_loss = 0

    # ---- placement and selling (Board + proposed 3b economy) ----
    def can_place(self, c):
        if self.gold < self.cost: return 'NotEnoughGold'  # 3b plan: gold is checked before the board rule
        if not (0 <= c[0] < W and 0 <= c[1] < H): return 'OutOfBounds'
        if c == SPAWN or c == EXIT: return 'SpawnOrExit'
        if c in self.blocked: return 'CellBlocked'
        for e in self.enemies:
            if e.frm == c or e.to == c: return 'EnemyInTheWay'
        self.blocked.add(c); self.field.compute()
        ok = self.field.dist(SPAWN) != UNREACH and all(self.field.dist(e.to) != UNREACH for e in self.enemies)
        self.blocked.discard(c); self.field.compute()
        return 'Placed' if ok else 'WouldBlockPath'

    def place(self, c):
        r = self.can_place(c)
        if r != 'Placed': return r
        self.blocked.add(c); self.field.compute()
        self.towers.append(Tower(c, self.ts, self.cost)); self.gold -= self.cost; self.spent += self.cost
        return r

    def sell(self, c):
        for i, t in enumerate(self.towers):
            if t.cell == c:
                back = t.cost if not t.fired else t.cost * self.refund_pct // 100
                self.towers.pop(i); self.blocked.discard(c); self.field.compute()
                self.gold += back; self.sold_loss += t.cost - back
                return back
        return None

    # ---- Battle.Tick ----
    def tick(self):
        if self.outcome != 'Running': return
        self.tick_n += 1
        e = self.waves.step()
        if e is not None: self.enemies.append(Enemy(self.field, *e))
        i = 0
        while i < len(self.enemies):
            en = self.enemies[i]
            if en.step():
                en.on = False; self.enemies.pop(i); self.leaks += 1
                if self.lives > 0: self.lives -= 1
            else: i += 1
        for t in self.towers:
            t.cd = f(t.cd - DT)
            if t.cd > 0: continue
            best = None; bd = float('inf'); cx, cy = t.cell
            for en in self.enemies:
                dx = f(en.x - cx); dy = f(en.y - cy)
                if f(f(dx * dx) + f(dy * dy)) > t.rng2: continue
                d = en.dte()
                if d < bd: best = en; bd = d
            if best is None: t.cd = 0.0; continue
            self.proj.append([float(cx), float(cy), best, t.dmg, t.ps]); t.fired = True
            t.cd = f(t.cd + t.cd_add)
        i = 0
        while i < len(self.proj):
            p = self.proj[i]; en = p[2]
            if not en.on: self.proj.pop(i); continue
            dx = f(en.x - p[0]); dy = f(en.y - p[1]); st = f(p[4] * DT); d2 = f(f(dx * dx) + f(dy * dy))
            if d2 <= f(st * st):
                p[0] = en.x; p[1] = en.y
                en.hp = f(en.hp - p[3]); self.proj.pop(i)
                if en.hp <= 0 and en.on:
                    en.on = False; self.enemies.remove(en); self.kills += 1
                    self.gold += en.bounty; self.earned += en.bounty
                continue
            sc = f(st / f(math.sqrt(d2)))
            p[0] = f(p[0] + f(dx * sc)); p[1] = f(p[1] + f(dy * sc)); i += 1
        if self.lives == 0: self.outcome = 'Lost'; return
        if self.waves.done and not self.enemies: self.outcome = 'Won'; self.proj.clear()
