# Checks the multi-group port against step 4b: building nothing and the 4b reference order (ref4_w8_k2.json) on the 4b numbers.
import json
from econ import *
from proposal4b import PROP
REF = [(t, tuple(c), a, e) for t, c, a, e in json.load(open(os.path.join(HERE, 'ref4_w8_k2.json')))]
b = Battle(mk_waves(PROP), 10, TS, gold=115, cost=40, refund_pct=50); g = b.gold; pays = []
while b.outcome == 'Running':
    b.tick()
    if b.gold != g: pays.append((b.tick_n, b.gold - g)); g = b.gold
print('4b, builds nothing:', b.outcome, b.tick_n, b.gold, pays, 'expect Lost 2931 175 [(1672, 40), (2332, 20)]')
r = run(PROP, Replay(REF))
print('4b reference:', r['out'], r['end_tick'], r['lives'], r['gold'], r['nL'], r['nS'], r['first_kill'], r['bad'], 'expect Won 10165 10 105 16 12 1288 []')
