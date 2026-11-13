# The proposal of docs/balance/0001-prototype-economy.md (step 4e: waves of mixed elements). The 4b numbers are in proposal4b.py.
from econ import P
# Waves 1 to 3: one element each, exactly as in step 4b: (element, count, health).
FIRST = [('S', 3, 45), ('L', 4, 55), ('L', 5, 70)]
# Waves 4 to 10: the entry order, one letter per enemy (a run of equal letters is one group), and the health per element.
# Damage per hit is 20 against the other element, so only ceil(health / 20) counts: every health from wave 4 on is a whole number of hits.
MIX = [('SSSLLL', 100), ('LLLLSSS', 100), ('SLSLSLSL', 180), ('SSLSSLSSL', 180),
       ('LLSLLSLLSL', 260), ('SLSLSLSLSLS', 360), ('LSLSLSLSLSLS', 420)]
BONUS = [40, 20, 25, 30, 35, 40, 45, 50, 55, 60]      # survival bonus per wave; the last pays on the winning tick
S, C, G, REFUND, LIVES = 115, 40, 10, 50, 10
DELAYS = [15] + [8] * 9


def runs(pattern):
    out = []
    for ch in pattern:
        if out and out[-1][0] == ch: out[-1][1] += 1
        else: out.append([ch, 1])
    return out


def hp_of(h, e): return h[e] if isinstance(h, dict) else h


def waves_of(first=FIRST, mix=MIX, bonus=BONUS, g=G, delays=DELAYS, hpmul=None):
    """econ wave specs. hpmul(i, hp) scales health of wave index i (rounded to 5)."""
    sc = (lambda i, h: h) if hpmul is None else (lambda i, h: int(round(hpmul(i, h) / 5)) * 5)
    out = [(c, 1, delays[i], sc(i, hp), g, e, bonus[i]) for i, (e, c, hp) in enumerate(first)]
    for j, (pat, h) in enumerate(mix):
        i = len(first) + j; rs = runs(pat)
        if len(rs) == 1: e, c = rs[0]; out.append((c, 1, delays[i], sc(i, hp_of(h, e)), g, e, bonus[i]))
        else: out.append(([(c, 1, sc(i, hp_of(h, e)), g, e) for e, c in rs], delays[i], bonus[i]))
    return out


def params(first=FIRST, mix=MIX, bonus=BONUS, S=S, C=C, g=G, refund_pct=REFUND, lives=LIVES, delays=DELAYS, hpmul=None):
    return dict(P, lives=lives, start_gold=S, cost=C, refund_pct=refund_pct,
                waves=waves_of(first, mix, bonus, g, delays, hpmul))


def assets(first=FIRST, mix=MIX):
    """The distinct enemy assets, Enemy_<Element><Health>, in order of first use."""
    out = []
    for e, c, hp in first:
        if (e, hp) not in out: out.append((e, hp))
    for pat, h in mix:
        for e, _ in runs(pat):
            if (e, hp_of(h, e)) not in out: out.append((e, hp_of(h, e)))
    return out


PROP = params()
