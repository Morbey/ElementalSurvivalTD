# Checks the eight-direction port against plan 0006 (3c-AC1..AC5 pins) and, tick by tick, against the tech-lead's core8.py.
import sys, os, random, importlib.util
from core import *
fl = Field(set(WALLS)); r = fl.route()
print('spawn distance', fl.dist(SPAWN), round(fl.dist(SPAWN) / 408, 3), 'steps', len(r) - 1,
      'diagonal', sum(1 for a, b in zip(r, r[1:]) if a[0] != b[0] and a[1] != b[1]))
print('route', r)
PLAN = [(0,4),(1,4),(2,5),(3,6),(4,7),(5,7),(6,7),(7,6),(8,5),(9,4),(9,3),(9,2),(9,1),(10,1),(11,1),(12,1),(13,2),(14,3),(15,4)]
print('route equals plan', r == PLAN)
b = Battle([((2.0, 100.0, 0), 1, 1, 0.0)], 10, dict(range=2.5, damage=10, sps=1.5, pspeed=10))
b.tick(); e = b.enemies[0]; d0 = e.dte(); n = 1; worst = 0; prev = d0
while b.enemies:
    b.tick(); n += 1
    if b.enemies: cur = b.enemies[0].dte(); worst = max(worst, abs((prev - cur) - f(2 * DT))); prev = cur
print('first DistanceToExit', d0, 'leaves on its tick', n, 'max deviation of the per-tick drop', worst)
b2 = Battle([((2.0,100.0,0),1,1,0.0)],10,{'range':2.5,'damage':10,'sps':1.5,'pspeed':10}); b2.place((5,7)); print('tower on (5,7): spawn', b2.field.dist(SPAWN))
# whole runs on the 3b numbers
sys.path.insert(0, '../sim3b'); 
TS = dict(range=2.5, damage=10, sps=1.5, pspeed=10)
HP3B = [45, 55, 65, 75, 90, 100, 115, 130, 150, 175]
W3B = [((2.0, HP3B[i], 10), c, 1, 15 if i == 0 else 8) for i, c in enumerate(range(3, 13))]
REF3B = [(0,(0,8),'buy'),(0,(0,8),'sell'),(0,(2,5),'buy'),(0,(3,5),'buy'),(1740,(3,6),'buy'),(2460,(6,6),'buy'),
       (3210,(7,6),'buy'),(3930,(8,5),'buy'),(4350,(2,5),'sell'),(4350,(3,5),'sell'),(4350,(9,3),'buy'),(4350,(8,2),'buy'),
       (5280,(6,5),'buy'),(5820,(7,5),'buy'),(6240,(8,3),'buy'),(6840,(8,6),'buy'),(7260,(9,2),'buy'),(7890,(2,5),'buy'),
       (8280,(3,5),'buy'),(8820,(13,2),'buy'),(9300,(14,2),'buy')]
def play(B, acts, trace=False):
    b = B(W3B, 10, TS, gold=100, cost=50, refund_pct=50); k = 0; tr = []; res = []
    acts = sorted(acts, key=lambda a: a[0])
    while b.outcome == 'Running':
        while k < len(acts) and acts[k][0] == b.tick_n:
            t, c, a = acts[k]; res.append(b.place(c) if a == 'buy' else b.sell(c)); k += 1
        b.tick()
        if trace: tr.append((b.lives, b.gold, tuple((e.x, e.y, e.hp) for e in b.enemies)))
    return b.outcome, b.tick_n, b.lives, b.gold, res, tr
print('3b numbers, nothing:', play(Battle, [])[:4])
print('3b numbers, 3b reference:', play(Battle, REF3B)[:5])
# tick-by-tick against core8.py
sys.path.insert(0, '../plan3c'); import core8
rng = random.Random(7); diffs = 0
free = [(x, y) for x in range(W) for y in range(H) if (x, y) not in WALLS and (x, y) not in (SPAWN, EXIT)]
for trial in range(12):
    acts = sorted([(rng.randrange(0, 6000) // 30 * 30, rng.choice(free), rng.choice(['buy', 'buy', 'buy', 'sell'])) for _ in range(25)])
    a = play(Battle, acts, True); bb = play(core8.Battle, acts, True)
    same = a[:5] == bb[:5] and a[5] == bb[5]; diffs += not same
    print('trial', trial, a[:4], 'same as core8' if same else ('DIFF', bb[:4]))
print('trials differing from core8:', diffs)
# the 3c proposal (before slice 4): building nothing and the 3c reference order, as the real 3c Core gave them
import json; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); sys.modules.pop("econ", None); import econ
from search import params as params3c
P3C = params3c([45, 55, 65, 75, 90, 100, 130, 175, 230, 305], S=115, C=40, g=10, refund_pct=50)
REF3C = [(t, tuple(c), a) for t, c, a in json.load(open(os.path.join(econ.HERE, 'ref3c.json')))]
r = econ.run(P3C, econ.Nothing()); print('3c, builds nothing:', r['out'], r['end_tick'], 'expect Lost 2931')
r = econ.run(P3C, econ.Replay(REF3C)); print('3c reference:', r['out'], r['end_tick'], r['lives'], r['gold'], r['towers'], 'expect Won 10136 10 45 20')
