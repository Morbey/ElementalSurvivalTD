# Checks the step 5b port (core.py, rules 16 to 24) against the three pins of plan 0012: generator, modifier, pause.
# python3 verify5b.py   -> each pin with what the plan expects, then 'ALL PINS PASS' or the failures.
import struct
from core import *

fails = []
def check(name, got, want):
    ok = got == want; print(f"{'ok  ' if ok else 'FAIL'} {name}: {got}" + ('' if ok else f'   expected {want}'))
    if not ok: fails.append(name)
def bits(x): return struct.unpack('<I', struct.pack('<f', x))[0]
def refused(fn, *a):
    try: fn(*a); return False
    except ValueError: return True

# ---- generator pin ----
r = Rng(1); check('seed 1, Next() x3', [f'{r.next():08X}' for _ in range(3)], ['96A0F96B', '12BC8390', '971E9964'])
r = Rng(1); check('seed 1, pool 5, draws 1-4', [draw(r, 5) for _ in range(4)], [(2, 1, 3), (2, 0, 4), (4, 2, 1), (3, 2, 1)])
r = Rng(2); check('seed 2, pool 5, draws 1-4', [draw(r, 5) for _ in range(4)], [(3, 1, 2), (2, 3, 4), (2, 1, 4), (0, 1, 4)])
check('seed 0, first Next()', f'{Rng(0).next():08X}', '92CA2F0E')

# ---- modifier pin ----
POOL = [perk(DMG, MUL, 1.25, 'L'), perk(DMG, MUL, 1.25, 'S'), perk(SPS, MUL, 1.15), perk(RANGE, ADD, 0.5), perk(GOLD, ADD, 2)]
TS = dict(range=2.5, damage=10, sps=1.5, pspeed=10)
m = Modifiers(); m.add(POOL[2], 2); check('rate after perk 2 alone (bits)', f'{bits(m.apply(SPS, "L", 1.5)):08X}', '3FDCCCCC')
m = Modifiers()
for k in (0, 2, 3, 0, 2): m.add(POOL[k], k)
for e, dmg in (('L', 15.625), ('S', 10.0)):
    t = Tower((0, 0), TS, 40, e, m)
    check(f'{e} after 0,2,3,0,2: damage, shots/s bits, range, cooldown added', (t.dmg, f'{bits(t.sps):08X}', t.r, round(t.cd_add, 7)), (dmg, '3FFDEB84', 3.0, 0.5040958))
check('gold per kill after the five picks', m.whole(GOLD, 10), 10)
m2 = Modifiers(); m2.add(modifier(DMG, MUL, 1.5)); m2.add(modifier(DMG, ADD, 2)); check('rule 17: base 10, x1.5 added before +2', m2.apply(DMG, 'L', 10), 18.0)
check('Core refuses gold x2, gold +0.5, gold with an element', [refused(modifier, GOLD, MUL, 2.0), refused(modifier, GOLD, ADD, 0.5), refused(modifier, GOLD, ADD, 2, 'L')], [True] * 3)
check('the perk data refuses +0 and x1; Core does not', [refused(perk, DMG, ADD, 0.0), refused(perk, DMG, MUL, 1.0), refused(modifier, DMG, MUL, 1.0)], [True, True, False])

# ---- pause pin ----
SH = (2.0, 45.0, 10, 'S', 10)
waves = [(SH, 1, 1.0, 0.0, 7), (SH, 2, 1.0, 2.0, 9)]
PP = [perk(DMG, MUL, 1.25, 'L'), perk(RANGE, ADD, 0.5), perk(GOLD, ADD, 2)]
check('pools of 1 and 2 refused', [refused(Battle, waves, 10, TS, 0, 0, 100, (), PP[:k]) for k in (1, 2)], [True, True])
b = Battle(waves, 10, TS, gold=40, cost=40, refund_pct=50, levels=[10, 10, 10], perks=PP, seed=2)
check('buy Light on (2,3)', b.place((2, 3), 'L'), 'Placed')
launches = []; lands = []; picks = {95: 2, 169: 1}; ev = {}
while b.outcome == 'Running':
    s0 = b.shots; p0 = list(b.proj); n0 = sum(b.spawned); b.tick(); t = b.tick_n
    if sum(b.spawned) > n0: ev.setdefault('enters', []).append(t)
    if b.shots > s0: launches.append(t)
    for p in p0:
        if p not in b.proj: lands.append((t, p[3]))
    if b.choice_open:
        ev.setdefault('choices', []).append((t, b.gold, b.xp, b.level, b.offer, round(b.waves.timer, 4)))
        if t == 95:  # any number of further Tick() calls change nothing
            snap = lambda: (b.tick_n, b.gold, b.waves.timer, b.shots, len(b.proj), [(e.x, e.y, e.hp) for e in b.enemies], [x.cd for x in b.towers])
            s1 = snap()
            for _ in range(50): b.tick()
            check('tick 95: 50 more Tick() calls move nothing', snap(), s1)
        k = b.pick(picks[t]); ev.setdefault('picked', []).append((t, k, b.towers[0].dmg))
check('entries', ev['enters'], [1, 121, 181])
check('launches', launches, [1, 40, 80, 121, 160, 200, 240])
check('hits (tick, damage)', lands, [(12, 20.0), (49, 20.0), (95, 20.0), (132, 25.0), (169, 25.0), (208, 25.0), (251, 25.0)])
check('choices opened', len(ev['choices']), 2)
check('choice on 95 (tick, gold, XP, level, offer, seconds to next wave)', ev['choices'][0], (95, 17, 10, 2, (2, 1, 0), 0.4167))
check('choice on 169 (tick, gold, XP, level, offer)', ev['choices'][1][:5], (169, 27, 20, 3, (1, 2, 0)))
check('picks (tick, perk, Light damage after)', ev['picked'], [(95, 0, 12.5), (169, 2, 12.5)])
check('outcome (result, tick, lives, gold, XP, level, choice open, opened)', (b.outcome, b.tick_n, b.lives, b.gold, b.xp, b.level, b.choice_open, b.choices_opened),
      ('Won', 251, 10, 48, 30, 4, False, 2))
print('ALL PINS PASS' if not fails else f'FAILED: {fails}')
