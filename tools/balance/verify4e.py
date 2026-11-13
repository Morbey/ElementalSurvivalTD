# Checks the multi-group port against the two pins of plan 0010 (schedule pin, mixed-fire pin).
from core import *
# Schedule pin: wave 1 = delay 1 s, 2 Light every 1 s then 2 Shadow every 0.5 s; wave 2 = delay 2 s, 1 Light.
L45 = (2.0, 45.0, 10, 'L'); S45 = (2.0, 45.0, 10, 'S')
w = Waves([([(L45, 2, 1.0), (S45, 2, 0.5)], 4, None, 1.0, 0), (L45, 1, 1.0, 2.0, 0)])
rows = []
for t in range(1, 400):
    e = w.step()
    if e: rows.append((t, e[0][3], e[1] + 1, w.started, w.i, round(w.timer, 4) if w.n == 0 and not w.done else 0, 'finished' if w.done else ''))
print('schedule pin (tick, element, wave, started, entered, seconds to next wave):')
for r in rows: print('  ', r)
print('  expect ticks 61 L, 121 L, 151 S, 181 S (entered 1, 2 s less one tick), 301 L wave 2 (entered 2, finished)')
# Mixed-fire pin: one wave, delay 0, bonus 7: 1 Light, then 1 Shadow after 1 s. A Light tower (cost 40) on (2,3), 40 gold.
TS = dict(range=2.5, damage=10, sps=1.5, pspeed=10)
b = Battle([([(L45, 1, 1.0), (S45, 1, 1.0)], 2, None, 0.0, 7)], 10, TS, gold=40, cost=40, refund_pct=50)
route0 = b.field.route(); print('place (2,3):', b.place((2, 3), 'L'), 'route unchanged', b.field.route() == route0, 'gold', b.gold)
hp = {}; lives = b.lives; ev = []
while b.outcome == 'Running':
    n0 = len(b.enemies); s0 = b.shots; p0 = list(b.proj); b.tick(); t = b.tick_n
    if len(b.enemies) > n0 and b.enemies[-1].hp == 45: ev.append((t, 'enters', b.enemies[-1].elem))
    if b.shots > s0:
        tg = b.proj[-1][2]; others = [(en.elem, round(en.x, 2), round(en.y, 2)) for en in b.enemies if en is not tg]
        ev.append((t, 'launch at', tg.elem, (round(tg.x, 2), round(tg.y, 2)), 'others on board', others))
    for p in p0:
        if p not in b.proj and p[2].on: ev.append((t, 'lands on', p[2].elem, (round(p[2].x, 2), round(p[2].y, 2)), 'health', p[2].hp))
    if b.lives != lives: lives = b.lives; ev.append((t, 'exit', 'lives', lives, 'gold', b.gold))
for e in ev: print('  ', e)
print('outcome', b.outcome, b.tick_n, 'gold', b.gold, 'launches', b.shots, 'wasted', b.wasted, 'recoverable', b.recoverable, 'kills', b.kills)
print('expect: launches 1, 40, 80, 120; lands 12, 49, 95 on Light (45); 131 on Shadow at (1.97, 4.97) -> 25; exits 652 (9) and 712 (8); Won, 7 gold; 4 launches, 3 wasted, 1 recoverable, no kill')
