# Prototype economy on the eight-direction port: player bots. Deterministic (Sloppy and Random use seeded RNGs).
# Slice 4: a bot = a cell policy (the classes below) + an element policy (`ep`, see ELEMENT POLICIES).
import sys, os, json, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from core import *

HERE = os.path.dirname(os.path.abspath(__file__))
TS = dict(range=2.5, damage=10, sps=1.5, pspeed=10)
SPEED = 2.0
# per wave: (count, interval s, delay s, enemy health, gold per kill[, element, survival bonus])
P = dict(lives=10, start_gold=100, cost=50, refund_pct=50,
         waves=[(c, 1, 15 if i == 0 else 8, 100, 10) for i, c in enumerate(range(3, 13))])
REACT = 30  # a bot acts at most every 0.5 s


def mk_waves(p):
    out = []
    for w in p['waves']:
        c, i, d, hp, g = w[:5]; el = w[5] if len(w) > 5 else None; bon = w[6] if len(w) > 6 else 0
        out.append(((SPEED, hp, g, el), c, i, d, bon))
    return out


# ---- ELEMENT POLICIES: callables (battle, bot) -> 'L' | 'S' | None ----
def counter(e): return None if e is None else ('S' if e == 'L' else 'L')
def wave_elem(b, k): return b.waves.w[k][0][3]
def announced(b):
    k = b.waves.next_index(); return wave_elem(b, k if k is not None else len(b.waves.w) - 1)
class Reads:
    """Buys the tower that damages the announced wave (the last wave once all have entered)."""
    name = 'reads'
    def __call__(self, b, bot): return counter(announced(b))
class ReadsBoard:
    """Reads the announcement and the board: the enemy health of each element on the board plus the announced wave's,
    per tower that can damage it; buys the counter of the element with the most health per tower (the announced one on a tie)."""
    name = 'reads (board + announcement)'
    def __call__(self, b, bot):
        need = {'L': 0.0, 'S': 0.0}
        for en in b.enemies: need[en.elem] += en.hp
        k = b.waves.next_index()
        if k is not None: w = b.waves.w[k]; need[w[0][3]] += w[0][1] * w[1]
        sup = {e: 1 + sum(t.elem == counter(e) for t in b.towers) for e in 'LS'}
        a = announced(b); o = counter(a)
        return counter(a if need[a] / sup[a] >= need[o] / sup[o] else o)
class ReadsShare:
    """Reads the announcement and the wave before it: aims for counter towers in proportion to the total health of the last wave
    that has entered and of the announced one (the last wave alone once all have entered), and buys the element furthest below its share."""
    name = 'reads (share of the last and the announced wave)'
    def __call__(self, b, bot):
        k = b.waves.next_index(); last = (k if k is not None else len(b.waves.w)) - 1
        need = {'L': 0.0, 'S': 0.0}
        for j in [x for x in (last, k) if x is not None and x >= 0]:
            w = b.waves.w[j]; need[w[0][3]] += w[0][1] * w[1]
        tot = need['L'] + need['S']; n = len(b.towers) + 1
        have = {e: sum(t.elem == counter(e) for t in b.towers) for e in 'LS'}
        a = announced(b); o = counter(a)
        gap = lambda e: need[e] / tot * n - have[e]
        return counter(a if gap(a) >= gap(o) else o)
class ReadsAhead:
    """Hypothetical (not a slice 4 rule): sees the announced wave and the n-1 after it (n = 10: has learned the whole order);
    aims for counter towers in proportion to their total health and buys the element furthest below its share."""
    def __init__(self, n): self.n = n; self.name = f'reads {n} waves ahead (hypothetical)'
    def __call__(self, b, bot):
        k = b.waves.next_index()
        if k is None: return counter(announced(b))
        need = {'L': 0.0, 'S': 0.0}
        for j in range(k, min(k + self.n, len(b.waves.w))):
            w = b.waves.w[j]; need[w[0][3]] += w[0][1] * w[1]
        tot = need['L'] + need['S']; n = len(b.towers) + 1
        have = {e: sum(t.elem == counter(e) for t in b.towers) for e in 'LS'}
        a = announced(b); o = counter(a); gap = lambda e: need[e] / tot * n - have[e]
        return counter(a if gap(a) >= gap(o) else o)
class Mono:
    def __init__(self, e): self.e = e; self.name = f'only {e}'
    def __call__(self, b, bot): return self.e
class Random:
    def __init__(self, seed): self.r = random.Random(10_000 + seed); self.name = f'random elements (seed {seed})'
    def __call__(self, b, bot): return self.r.choice('LS')
class Half:
    """Ignores the announcement: buys whichever element it has fewer of, `first` on a tie."""
    def __init__(self, first='L'): self.first = first; self.name = f'half and half ({first} first)'
    def __call__(self, b, bot):
        nl = sum(t.elem == 'L' for t in b.towers); ns = len(b.towers) - nl
        return self.first if nl == ns else ('L' if nl < ns else 'S')
class NoElem:
    name = 'no element (3c)'
    def __call__(self, b, bot): return None


FREE_CELLS = [(x, y) for y in range(H) for x in range(W) if (x, y) not in WALLS and (x, y) not in (SPAWN, EXIT)]
_EXP = {}


def expmap(route):
    """Cells of path within range of a tower on each cell, for this route (sampled every 1/8 cell)."""
    key = tuple(route)
    m = _EXP.get(key)
    if m is not None: return m
    m = {}
    for a, b in zip(route, route[1:]):
        L = step_len(a, b); n = int(round(L * 8))
        for i in range(n):
            t = (i + 0.5) / n; sx = a[0] + (b[0] - a[0]) * t; sy = a[1] + (b[1] - a[1]) * t
            for x in range(int(sx) - 3, int(sx) + 4):
                for y in range(int(sy) - 3, int(sy) + 4):
                    if (x - sx) ** 2 + (y - sy) ** 2 <= 6.25: m[(x, y)] = m.get((x, y), 0.0) + L / n
    if len(_EXP) > 20000: _EXP.clear()
    _EXP[key] = m
    return m


def exposure(route, towers):
    m = expmap(route); return sum(m.get(t, 0.0) for t in towers)


def side_cells(route):
    s = set()
    for a, b in zip(route, route[1:]):
        if a[0] != b[0] and a[1] != b[1]: s.add((b[0], a[1])); s.add((a[0], b[1]))
    return s


def candidates(b, maze):
    """(exposure after placement, cell) for every free cell. maze=False skips cells on the route."""
    route = b.field.route(); rs = set(route); sides = side_cells(route); towers = [t.cell for t in b.towers]
    m = expmap(route); base = exposure(route, towers); out = []
    for c in FREE_CELLS:
        if c in b.blocked: continue
        if c in rs and not maze: continue
        if c in rs or c in sides:  # the route can change only through these cells
            b.blocked.add(c); b.field.compute()
            if b.field.dist(SPAWN) != UNREACH: out.append((exposure(b.field.route(), towers + [c]), c))
            b.blocked.discard(c); b.field.compute()
        else:
            out.append((base + m.get(c, 0.0), c))
    return out


def best_first(cands): return [c for s, c in sorted(cands, key=lambda x: (-x[0], x[1]))]


class Bot:
    name = 'base'
    ep = NoElem()
    def __init__(self): self.log = []
    def wants(self, b): return b.gold >= b.cost
    def pick(self, b): return []
    def buy(self, b, cells, elem=0):
        e = self.ep(b, self) if elem == 0 else elem
        for c in cells:
            if b.place(c, e) == 'Placed': self.log.append((b.tick_n, c, 'buy', -b.cost, b.gold, e)); return True
        return False
    def act(self, b):
        while self.wants(b):
            if not self.buy(b, self.pick(b)): return


class Nothing(Bot):
    name = 'builds nothing'
    def wants(self, b): return False


class Sensible(Bot):
    """Each tower on the free cell off the route that adds most path in range (a lane beside it may become an L)."""
    name = 'sensible (beside the route, reinvests at once)'
    def pick(self, b): return best_first(candidates(b, False))


STRONG_ORDER = None


def strong_order():
    global STRONG_ORDER
    if STRONG_ORDER is None:
        STRONG_ORDER = [tuple(c) for c in json.load(open(os.path.join(HERE, 'strong_order.json')))]
    return STRONG_ORDER


class Strong(Bot):
    """Mazes: follows an expert order found offline (two-step lookahead on path in range, see order.py)."""
    name = 'maze builder (expert order, reinvests at once)'
    def pick(self, b):
        own = {t.cell for t in b.towers}; return [c for c in strong_order() if c not in own]


class Far(Bot):
    name = 'bad: towers far from the route'
    def pick(self, b): return [c for s, c in sorted(candidates(b, False)) if s - exposure(b.field.route(), [t.cell for t in b.towers]) == 0]


class Poor(Bot):
    """Spends at once, but each tower reaches at most 1.5 cells of path."""
    name = 'bad: towers at the edge of range'
    def pick(self, b):
        base = exposure(b.field.route(), [t.cell for t in b.towers])
        return [c for s, c in sorted(candidates(b, False), key=lambda x: (-x[0], x[1])) if 0 < s - base <= 1.5]


class Late(Sensible):
    def __init__(self, start_wave=3): super().__init__(); self.w = start_wave; self.name = f'late: first tower at wave {start_wave}'
    def wants(self, b): return b.waves.started >= self.w and b.gold >= b.cost


class Hoarder(Sensible):
    """Spends the starting gold, then buys only in batches of k towers."""
    def __init__(self, k=3): super().__init__(); self.k = k; self.batch = 0; self.name = f'saver: 2 at start, then batches of {k}'
    def wants(self, b):
        if b.tick_n == 0 and b.gold >= b.cost: return True
        if self.batch == 0 and b.gold >= self.k * b.cost: self.batch = self.k
        if self.batch > 0 and b.gold >= b.cost: self.batch -= 1; return True
        return False


class Mover(Sensible):
    """Sensible, but when wave `w`'s first enemy enters, sells its first two towers (both have fired) and rebuilds them where they add most."""
    def __init__(self, w=5): super().__init__(); self.w = w; self.done = False; self.name = f'sensible, sells and rebuilds 2 towers at wave {w}'
    def act(self, b):
        if not self.done and b.waves.started >= self.w:
            self.done = True
            for c in [t.cell for t in b.towers[:2]]:
                back = b.sell(c); self.log.append((b.tick_n, c, 'sell', back, b.gold, None))
            sold = {e[1] for e in self.log if e[2] == 'sell'}
            for _ in range(2): self.buy(b, [c for c in best_first(candidates(b, True)) if c not in sold])
        super().act(b)


class Swapper(Sensible):
    """Reads the announcement; when wave `w`'s first enemy enters, sells its `k` oldest towers of the element that cannot
    touch that wave (they have fired) and rebuilds the other element where it adds most."""
    def __init__(self, w, k=2): super().__init__(); self.w = w; self.k = k; self.done = False; self.name = f'reads, swaps {k} towers at wave {w}'
    def act(self, b):
        if not self.done and b.waves.started >= self.w:
            self.done = True; useless = wave_elem(b, self.w - 1)
            cells = [t.cell for t in b.towers if t.elem == useless][:self.k]
            for c in cells:
                back = b.sell(c); self.log.append((b.tick_n, c, 'sell', back, b.gold, useless))
            for _ in cells: self.buy(b, [c for c in best_first(candidates(b, True)) if c not in cells], counter(useless))
        super().act(b)


class Sloppy(Sensible):
    """A human proxy: acts every `every` s and takes a seeded-random cell among the `k` best sensible ones."""
    def __init__(self, seed, k=6, every=2.0):
        super().__init__(); self.rng = random.Random(seed); self.k = k; self.every = int(every * 60)
        self.name = f'sloppy human (top {k}, {every:g} s), seed {seed}'
    def act(self, b):
        if b.tick_n % self.every == 0: super().act(b)
    def pick(self, b):
        top = super().pick(b)[:self.k]; self.rng.shuffle(top); return top


class Replay(Bot):
    """Plays a fixed table of (tick, cell, 'buy'|'sell'); records refusals."""
    name = 'reference order'
    def __init__(self, table, shift=0):  # table rows: (tick, cell, 'buy'|'sell', element)
        super().__init__(); self.t = sorted(((r[0] + (shift if r[0] else 0), i, r[1], r[2], r[3] if len(r) > 3 else None) for i, r in enumerate(table))); self.k = 0; self.bad = []
    def act(self, b):
        while self.k < len(self.t) and self.t[self.k][0] <= b.tick_n:
            _, _, c, a, e = self.t[self.k]; self.k += 1
            if a == 'buy':
                r = b.place(c, e)
                if r != 'Placed': self.bad.append((b.tick_n, c, r))
                else: self.log.append((b.tick_n, c, 'buy', -b.cost, b.gold, e))
            else:
                back = b.sell(c)
                if back is None: self.bad.append((b.tick_n, c, 'NoTower'))
                self.log.append((b.tick_n, c, 'sell', back, b.gold, e))


def run(p, bot, trace_s=(30, 60, 90, 120, 150), every=1, ep=None):
    if ep is not None: bot.ep = ep
    b = Battle(mk_waves(p), p['lives'], TS, gold=p['start_gold'], cost=p['cost'], refund_pct=p['refund_pct'])
    gold_at = {}; wave_start = {}; marks = {int(s * 60): s for s in trace_s}; first_kill = None
    react = 1 if isinstance(bot, Replay) else REACT
    while b.outcome == 'Running':
        if b.tick_n % react == 0: bot.act(b)
        st = b.waves.started
        if b.tick_n in marks: gold_at[marks[b.tick_n]] = b.gold
        b.tick()
        if first_kill is None and b.kills: first_kill = b.tick_n
        if b.waves.started > st: wave_start[b.waves.started] = (b.tick_n, len(b.towers), b.gold, b.lives,
                                                               sum(t.elem == 'L' for t in b.towers), sum(t.elem == 'S' for t in b.towers))
    return dict(bot=bot.name, out=b.outcome, t=round(b.tick_n / 60, 1), end_tick=b.tick_n, lives=b.lives, kills=b.kills,
                towers=len(b.towers), gold=b.gold, gold_at=gold_at, wave_start=wave_start, wave_reached=b.waves.started,
                route=round(b.field.dist(SPAWN) / 408, 2), sold_loss=b.sold_loss, log=bot.log, first_kill=first_kill,
                tower_cells=[t.cell for t in b.towers], bad=getattr(bot, 'bad', None), bonus=b.bonus_earned, earned=b.earned, leaks_w=b.leaks_w, shots=b.shots, wasted=b.wasted, recoverable=b.recoverable, busy=b.busy_ticks, mixed=b.mixed_ticks,
                nL=sum(t.elem == 'L' for t in b.towers), nS=sum(t.elem == 'S' for t in b.towers))
