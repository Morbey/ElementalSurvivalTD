# Float32-faithful port of ElementalTD.Core with the 3c eight-direction movement of plan 0006 (T3c-1) (Battle, Board, WaveSchedule, Enemy, PathFollower, FlowField, Projectile)
# as of commit aadb638 + slice 3a, plus the 3b economy (gold, cost, bounty, sell) as an optional layer.
# Step 4e: a wave may hold several groups (see Waves). Slice 4 (balance assumptions, docs/balance/0001): elements 'L'/'S' on towers and enemies (None = 3c behaviour),
# targeting is the 3c rule (no element in it); damage is applied at the hit: x2 against the other element, 0 against the own
# (a launch at an immune enemy is a wasted shot and counts as fired); a per-wave survival bonus paid on the tick
# the wave has finished spawning and none of its enemies is on the board, while lives > 0.
# Step 5a: XP and level as counters only (nothing reads them). An enemy stat tuple may carry its XP as a 5th item (default 0);
# `levels` lists the XP each level needs (the n-th item takes level n to n + 1), XP is a running total and carries over,
# the level starts at 1 and stops when the table ends. A kill adds its XP on the tick its shot lands.
# Step 5b (plan 0012, rules 16 to 24): modifiers (Modifiers), the generator and the draw (Rng, draw), and the perk choice that
# pauses the battle (Battle.perks / seed / choice_open / pick). A battle with an empty pool is the 5a battle on every tick.
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


STRAIGHT, DIAGONAL = 408, 577
DIAG_LEN = f(577.0 / 408.0)          # DiagonalCost / (float)StraightCost
DIRS = ((1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1))  # counter-clockwise from +x
TURNS = (0, 1, -1, 2, -2, 3, -3, 4)  # straight, 45 left, 45 right, 90 left, 90 right, 135 left, 135 right, back


class Field:
    def __init__(self, blocked):
        self.blocked = blocked; self.compute()

    def free(self, x, y): return 0 <= x < W and 0 <= y < H and (x, y) not in self.blocked

    def legal(self, x, y, k):
        dx, dy = DIRS[k]
        if not self.free(x + dx, y + dy): return False
        return (self.free(x + dx, y) and self.free(x, y + dy)) if dx and dy else True

    def compute(self):
        d = {}
        if EXIT not in self.blocked:
            d[EXIT] = 0; q = deque([EXIT]); inq = {EXIT}
            while q:
                c = q.popleft(); inq.discard(c); x, y = c; dc = d[c]
                for k in range(8):
                    if not self.legal(x, y, k): continue
                    dx, dy = DIRS[k]; n = (x + dx, y + dy); nd = dc + (DIAGONAL if dx and dy else STRAIGHT)
                    if nd < d.get(n, UNREACH):
                        d[n] = nd
                        if n not in inq: inq.add(n); q.append(n)
        self.d = d

    def dist(self, c): return self.d.get(c, UNREACH)

    def next(self, frm, came):
        cur = self.dist(frm)
        if cur == 0 or cur == UNREACH: return None
        hv = (frm[0] - came[0], frm[1] - came[1]); h = DIRS.index(hv) if hv != (0, 0) else 0
        for t in TURNS:
            k = (h + t) % 8
            if not self.legal(frm[0], frm[1], k): continue
            dx, dy = DIRS[k]; n = (frm[0] + dx, frm[1] + dy)
            if self.dist(n) + (DIAGONAL if dx and dy else STRAIGHT) == cur: return n
        return None

    def route(self):
        """Cells an enemy entering now walks, spawn to exit."""
        cells = [SPAWN]; frm = SPAWN; came = SPAWN
        while True:
            n = self.next(frm, came)
            if n is None: return cells
            cells.append(n); came, frm = frm, n


def step_len(a, b): return DIAG_LEN if (a[0] != b[0] and a[1] != b[1]) else 1.0


class Enemy:
    __slots__ = ('field', 'speed', 'hp', 'maxhp', 'frm', 'to', 'p', 'len', 'arrived', 'x', 'y', 'on', 'bounty', 'elem', 'wave', 'xp')

    def __init__(self, field, speed, hp, bounty, elem=None, wave=0, xp=0):
        self.xp = xp; self.elem = elem; self.wave = wave; self.field = field; self.speed = f(speed); self.hp = f(hp); self.maxhp = self.hp; self.bounty = bounty
        self.frm = SPAWN; n = field.next(SPAWN, SPAWN); self.to = n if n else SPAWN; self.len = step_len(self.frm, self.to)
        self.p = 0.0; self.arrived = self.frm == self.to and field.dist(SPAWN) == 0; self.on = True; self.sync()

    def sync(self):
        t = f(self.p / self.len)
        self.x = f(self.frm[0] + f((self.to[0] - self.frm[0]) * t))
        self.y = f(self.frm[1] + f((self.to[1] - self.frm[1]) * t))

    def dte(self):
        return 0.0 if self.arrived else f(f(self.field.dist(self.to) / f(float(STRAIGHT))) + f(self.len - self.p))

    def occupies(self, c):  # the rectangle spanned by From and To: 2 cells on a straight step, 4 on a diagonal
        return (min(self.frm[0], self.to[0]) <= c[0] <= max(self.frm[0], self.to[0]) and
                min(self.frm[1], self.to[1]) <= c[1] <= max(self.frm[1], self.to[1]))

    def step(self):
        if not (self.arrived or self.frm == self.to):
            adv = f(DT * self.speed)
            if adv > 0:
                self.p = f(self.p + adv)
                while self.p >= self.len:
                    self.p = f(self.p - self.len); left = self.frm; self.frm = self.to
                    n = self.field.next(self.frm, left)
                    if n is not None: self.to = n; self.len = step_len(self.frm, n)
                    else:
                        self.to = self.frm; self.p = 0.0; self.len = 1.0; self.arrived = self.field.dist(self.frm) == 0; break
        self.sync()
        return self.arrived


# ---- step 5b: modifiers (rules 16, 17, 19), the generator (rule 20) and the draw (rule 21) ----
DMG, SPS, RANGE, GOLD = 'damage', 'sps', 'range', 'gold'      # Stat: TowerDamage, TowerShotsPerSecond, TowerRange, GoldPerKill
ADD, MUL = 'add', 'mul'


def modifier(stat, op, value, elem=None):
    """(stat, element or None, operation, float32 amount), with Core's refusals (5b-AC1): gold per kill takes whole additions only."""
    if stat == GOLD and (op == MUL or value != int(value) or elem is not None): raise ValueError('gold per kill takes whole additions only')
    return (stat, elem, op, f(value))


def perk(stat, op, value, elem=None):
    """A perk asset's modifier, with the data's refusals (5b-AC8): an addition at or below 0, a multiplication at or below 1."""
    if op == ADD and value <= 0: raise ValueError('an addition at or below 0')
    if op == MUL and value <= 1: raise ValueError('a multiplication at or below 1')
    return modifier(stat, op, value, elem)


class Modifiers:
    """The run's modifiers in the order added. Rule 17: the base, every addition in order, then every multiplication in order, float32 after each."""
    def __init__(self): self.l = []
    def add(self, m, source=-1): self.l.append(m + (source,))
    def apply(self, stat, elem, v):
        v = f(v)
        for s, e, op, a, _ in self.l:
            if s == stat and op == ADD and (e is None or e == elem): v = f(v + a)
        for s, e, op, a, _ in self.l:
            if s == stat and op == MUL and (e is None or e == elem): v = f(v * a)
        return v
    def whole(self, stat, v): return v + sum(int(a) for s, _, op, a, _ in self.l if s == stat and op == ADD)


MASK = 0xFFFFFFFF


class Rng:
    """Rule 20 (SplitMix-style, 32-bit)."""
    def __init__(self, seed): self.s = seed & MASK
    def next(self):
        self.s = (self.s + 0x9E3779B9) & MASK; z = self.s
        z ^= z >> 16; z = (z * 0x85EBCA6B) & MASK; z ^= z >> 13; z = (z * 0xC2B2AE35) & MASK; z ^= z >> 16
        return z
    def below(self, n): return (self.next() * n) >> 32


def draw(rng, n):
    """Rule 21: a partial shuffle of 0..n-1, set afresh; the offer is slots 0 to 2."""
    order = list(range(n))
    for i in range(3):
        j = i + rng.below(n - i); order[i], order[j] = order[j], order[i]
    return tuple(order[:3])


class Tower:
    __slots__ = ('cell', 'rng2', 'dmg', 'cd_add', 'ps', 'cd', 'fired', 'cost', 'elem', 'ts', 'r', 'sps')

    def __init__(self, cell, ts, cost, elem=None, mods=None):
        self.elem = elem; self.cell = cell; self.ts = ts; self.ps = f(ts['pspeed']); self.cd = 0.0; self.fired = False; self.cost = cost
        self.refresh(mods if mods is not None else Modifiers())

    def refresh(self, mods):
        """Rule 18: the effective stats from the base ones; a shot in flight and a running cooldown keep theirs."""
        r = mods.apply(RANGE, self.elem, self.ts['range']); self.r = r; self.rng2 = f(r * r)
        self.dmg = mods.apply(DMG, self.elem, self.ts['damage'])
        self.sps = mods.apply(SPS, self.elem, self.ts['sps']); self.cd_add = f(1.0 / self.sps)


def groups(wave):
    """A wave's groups in spawn order, each (enemy, count, interval). A wave is (enemy, count, interval, delay[, bonus]) for one group,
    or ([groups], total count, None, delay[, bonus]) for several."""
    return wave[0] if isinstance(wave[0], list) else [(wave[0], wave[1], wave[2])]


class Waves:
    """Step 4e timing: a group's interval is the time before each of its enemies, its first included, except the wave's first enemy,
    which enters when the wave's delay ends. So after an entry the timer adds the next enemy's group interval, or the next wave's delay."""
    def __init__(self, waves):
        self.w = waves; self.g = [groups(w) for w in waves]
        self.i = 0; self.n = 0; self.gi = 0; self.gn = 0; self.timer = f(waves[0][3]); self.done = False; self.started = 0

    def next_index(self):
        """The wave the HUD announces: the next one whose first enemy has not entered; None once all have entered."""
        if self.done: return None
        k = self.i if self.n == 0 else self.i + 1
        return k if k < len(self.w) else None

    def step(self):
        if self.done: return None
        self.timer = f(self.timer - DT)
        if self.timer > 0: return None
        if self.timer < -DT: self.timer = -DT
        gs = self.g[self.i]; e = (gs[self.gi][0], self.i)
        if self.n == 0: self.started += 1
        self.n += 1; self.gn += 1
        if self.gn == gs[self.gi][1]: self.gi += 1; self.gn = 0
        if self.gi < len(gs):
            self.timer = f(self.timer + f(gs[self.gi][2])); return e
        self.i += 1; self.n = 0; self.gi = 0; self.gn = 0
        if self.i < len(self.w): self.timer = f(self.timer + f(self.w[self.i][3]))
        else: self.done = True
        return e


class Battle:
    def __init__(self, waves, lives, tower_stats, gold=0, cost=0, refund_pct=100, levels=(), perks=None, seed=0):
        self.blocked = set(WALLS); self.field = Field(self.blocked)
        self.towers = []; self.enemies = []; self.proj = []
        self.waves = Waves(waves); self.lives = lives; self.outcome = 'Running'; self.tick_n = 0
        self.ts = tower_stats; self.gold = gold; self.cost = cost; self.refund_pct = refund_pct
        self.kills = 0; self.leaks = 0; self.earned = 0; self.spent = 0; self.sold_loss = 0
        self.alive = [0] * len(waves); self.spawned = [0] * len(waves); self.total = [sum(g[1] for g in groups(w)) for w in waves]; self.paid = [False] * len(waves); self.leaks_w = [0] * len(waves); self.bonus_earned = 0; self.shots = 0; self.wasted = 0; self.recoverable = 0; self.busy_ticks = 0; self.mixed_ticks = 0
        self.levels = list(levels); self.xp = 0; self.level = 1; self.levelup_ticks = []; self.kill_log = []; self.paid_tick = [None] * len(waves)  # step 5a counters; kill_log: (tick, wave)
        # step 5b: the pool (a perk's place is its source), the generator, the levels owed a choice, the open offer
        self.perks = list(perks or []); self.mods = Modifiers(); self.rng = Rng(seed); self.pending = 0; self.answered = 0
        if 0 < len(self.perks) < 3: raise ValueError('a pool of one or two perks')
        self.choice_open = False; self.offer = None; self.choices_opened = 0; self.taken = [0] * len(self.perks)
        self.choice_log = []  # (tick, level, offer, slot)
        self.max_dmg = 0.0    # the largest damage of a launch: what a damage number can show
        n = len(waves); self.shots_w = [0] * n; self.wasted_w = [0] * n; self.recov_w = [0] * n; self.busy_w = [0] * n; self.mixed_w = [0] * n  # per wave of the target / of an enemy on the board

    # ---- placement and selling (Board + proposed 3b economy) ----
    def can_place(self, c):
        if self.gold < self.cost: return 'NotEnoughGold'  # 3b plan: gold is checked before the board rule
        if not (0 <= c[0] < W and 0 <= c[1] < H): return 'OutOfBounds'
        if c == SPAWN or c == EXIT: return 'SpawnOrExit'
        if c in self.blocked: return 'CellBlocked'
        for e in self.enemies:
            if e.occupies(c): return 'EnemyInTheWay'
        self.blocked.add(c); self.field.compute()
        ok = self.field.dist(SPAWN) != UNREACH and all(self.field.dist(e.to) != UNREACH for e in self.enemies)
        self.blocked.discard(c); self.field.compute()
        return 'Placed' if ok else 'WouldBlockPath'

    def place(self, c, elem=None):
        r = self.can_place(c)
        if r != 'Placed': return r
        self.blocked.add(c); self.field.compute()
        self.towers.append(Tower(c, self.ts, self.cost, elem, self.mods)); self.gold -= self.cost; self.spent += self.cost
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
    # ---- step 5b: the choice (rules 22, 23) ----
    def open_choice(self):
        if self.choice_open or not self.pending or not self.perks: return
        self.offer = draw(self.rng, len(self.perks)); self.choice_open = True; self.choices_opened += 1

    def pick(self, slot):
        if not self.choice_open or not 0 <= slot < 3: raise ValueError('no choice open or slot outside the offer')
        k = self.offer[slot]; self.mods.add(self.perks[k], k); self.taken[k] += 1
        self.choice_log.append((self.tick_n, self.answered + 2, self.offer, slot))
        for t in self.towers: t.refresh(self.mods)
        self.pending -= 1; self.answered += 1; self.choice_open = False; self.offer = None
        self.open_choice()
        return k

    def tick(self):
        if self.outcome != 'Running' or self.choice_open: return
        self.tick_n += 1
        e = self.waves.step()
        if e is not None:
            (st, wi) = e; self.enemies.append(Enemy(self.field, *st[:3], st[3] if len(st) > 3 else None, wi, st[4] if len(st) > 4 else 0))
            self.alive[wi] += 1; self.spawned[wi] += 1
        i = 0
        while i < len(self.enemies):
            en = self.enemies[i]
            if en.step():
                en.on = False; self.enemies.pop(i); self.leaks += 1; self.alive[en.wave] -= 1; self.leaks_w[en.wave] += 1
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
            if t.elem is None or best.elem is None: dmg = t.dmg
            else: dmg = 0.0 if best.elem == t.elem else f(t.dmg * 2.0)  # 0 against the own element, x2 against the other
            self.shots += 1; self.shots_w[best.wave] += 1
            if dmg > self.max_dmg: self.max_dmg = dmg
            if dmg == 0.0:  # wasted; 'recoverable' when an enemy it could damage was also in range (what skip-own-element would save)
                self.wasted += 1; self.wasted_w[best.wave] += 1
                for en in self.enemies:
                    if en.elem != t.elem:
                        dx = f(en.x - cx); dy = f(en.y - cy)
                        if f(f(dx * dx) + f(dy * dy)) <= t.rng2: self.recoverable += 1; self.recov_w[best.wave] += 1; break
            self.proj.append([float(cx), float(cy), best, dmg, t.ps]); t.fired = True
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
                    en.on = False; self.enemies.remove(en); self.kills += 1; self.alive[en.wave] -= 1
                    g = self.mods.whole(GOLD, en.bounty); self.gold += g; self.earned += g
                    self.xp += en.xp; self.kill_log.append((self.tick_n, en.wave))
                    while self.level - 1 < len(self.levels) and self.xp >= sum(self.levels[:self.level]):
                        self.level += 1; self.levelup_ticks.append(self.tick_n); self.pending += 1
                continue
            sc = f(st / f(math.sqrt(d2)))
            p[0] = f(p[0] + f(dx * sc)); p[1] = f(p[1] + f(dy * sc)); i += 1
        if self.enemies:
            self.busy_ticks += 1; e0 = self.enemies[0].elem; mixed = any(en.elem != e0 for en in self.enemies)
            if mixed: self.mixed_ticks += 1
            for k in {en.wave for en in self.enemies}:
                self.busy_w[k] += 1
                if mixed: self.mixed_w[k] += 1
        if self.lives > 0:
            for k, wv in enumerate(self.waves.w):
                if not self.paid[k] and self.spawned[k] == self.total[k] and self.alive[k] == 0:
                    self.paid[k] = True; self.paid_tick[k] = self.tick_n; bon = wv[4] if len(wv) > 4 else 0; self.gold += bon; self.bonus_earned += bon
        if self.lives == 0: self.outcome = 'Lost'
        elif self.waves.done and not self.enemies: self.outcome = 'Won'; self.proj.clear()
        if self.outcome == 'Running': self.open_choice()  # rule 22: after the bonuses and the outcome
