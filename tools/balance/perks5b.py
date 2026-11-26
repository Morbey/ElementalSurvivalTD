# Step 5b: the perk pool builder, the pick policies (pickers) and the job runner shared by explore5b.py, refrepair5b.py and report5b.py.
# A picker is a callable (battle) -> slot 0..2; econ.run calls it on every open choice, before any action on that tick.
import random
from p4 import *
from xp5a import XP, LEVELS
import proposal as pr

# ---- the pool, in order: a perk's place is its name in the draw ----
NAMES = ['Light damage', 'Shadow damage', 'Fire rate', 'Range', 'Gold per kill']
LD, SD, RATE, RNG_, GOLD_ = range(5)
NOP = ('none', None, ADD, 0.0)   # measurement only: a perk no rule reads


def pool(dmg, rate, rng, gold):
    return [perk(DMG, MUL, dmg, 'L'), perk(DMG, MUL, dmg, 'S'), perk(SPS, MUL, rate), perk(RANGE, ADD, rng), perk(GOLD, ADD, gold)]


def with_perks(p, perks, seed=None): return dict(p, xp=XP, levels=LEVELS, perks=perks, seed=seed)


# ---- what the pickers read ----
def next_need(b):
    """Enemy health on the board plus the announced wave's (the last wave once all have entered), per enemy element."""
    need = {'L': 0.0, 'S': 0.0}
    for en in b.enemies: need[en.elem] += en.hp
    k = b.waves.next_index()
    for e, v in wave_need(b, k if k is not None else len(b.waves.w) - 1).items(): need[e] += v
    return need


def power(b, elem): return sum(t.dmg * t.sps for t in b.towers if t.elem == elem)


def weaker_damage(b):
    """The damage perk of the element whose towers face the most health per damage per second (Light towers kill Shadow enemies)."""
    need = next_need(b); pl = power(b, 'L') or 1e-9; ps = power(b, 'S') or 1e-9
    return LD if need['S'] / pl >= need['L'] / ps else SD


def majority_damage(b):
    nl = sum(t.elem == 'L' for t in b.towers); return LD if nl >= len(b.towers) - nl else SD


# ---- pickers ----
class Pref:
    """Takes the first perk of the order on offer; order is a list of places or a callable (battle) -> list."""
    def __init__(self, order): self.order = order
    def __call__(self, b):
        for k in (self.order(b) if callable(self.order) else self.order):
            if k in b.offer: return b.offer.index(k)
        return 0


class RandomPick:
    def __init__(self, seed): self.r = random.Random(20_000 + seed)
    def __call__(self, b): return self.r.randrange(3)


class Then:
    """Takes `perk` when offered, else asks `other`."""
    def __init__(self, perk, other): self.perk = perk; self.other = other
    def __call__(self, b): return b.offer.index(self.perk) if self.perk in b.offer else self.other(b)


class Reads:
    """Reads the cards and the next wave, in the order of the measured worth (explore5b.py worth / stack): the damage of the element
    the next wave needs most per damage per second; gold per kill on the first two choices (levels 2 and 3), where it is worth about
    a damage pick and later nearly nothing; range if not yet taken (the first pick is worth fire rate, later ones less); fire rate;
    range; the other damage; gold."""
    def __call__(self, b):
        w = weaker_damage(b)
        order = [w] + ([GOLD_] if b.answered < 2 else []) + ([RNG_] if not b.taken[RNG_] else []) + [RATE, RNG_, LD + SD - w, GOLD_]
        return Pref(order)(b)


class AtLevel:
    """Measurement only (explore5b.py worth): with a pool [X, NOP, NOP], takes X on the choices of the given levels, NOP on the others."""
    def __init__(self, levels): self.levels = levels
    def __call__(self, b): return b.offer.index(0 if b.answered + 2 in self.levels else 1)


PICKERS = {  # the bots' pick policies, as the document names them
    'reads': 'reads the cards and the next wave (Reads)',
    'majority': 'always the damage of its majority element; else fire rate, range, the other damage, gold',
    'random': 'a seeded random slot',
    'gold': 'gold per kill when offered, else a random slot',
    'range': 'range when offered, else a random slot',
    'rate': 'fire rate when offered, else a random slot',
}


def picker(kind, seed=0):
    if isinstance(kind, tuple): return AtLevel(kind[1])
    if kind == 'reads': return Reads()
    if kind == 'random': return RandomPick(seed)
    if kind == 'gold': return Then(GOLD_, RandomPick(seed))
    if kind == 'range': return Then(RNG_, RandomPick(seed))
    if kind == 'rate': return Then(RATE, RandomPick(seed))
    if kind == 'majority': return Pref(lambda b: [majority_damage(b), RATE, RNG_, LD + SD - majority_damage(b), GOLD_])
    if kind == 'weaker': return Pref(lambda b: [weaker_damage(b), RATE, RNG_, LD + SD - weaker_damage(b), GOLD_])
    if kind == 'first': return lambda b: 0
    raise ValueError(kind)


def job(a):
    """(params, bot kind, bot arg, element policy, element arg, picker kind, seed) -> econ.run result. With a pool and no fixed
    p['seed'], the draw seed is the job's seed; crowd member s uses sloppy seed s, picker seed s and draw seed s."""
    p, kind, arg, e, earg, pk, s = a
    if p.get('perks') and p.get('seed') is None: p = dict(p, seed=s)
    pick = picker(pk, s) if p.get('perks') else None
    if kind == 'ref':
        from report4e import REF
        return run(p, Replay(arg or REF), picker=pick)
    return run(p, mk(kind, arg), ep=mkep(e, earg), picker=pick)
