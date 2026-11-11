# Reference order (slice 4): the maze order with reading elements, a tick-0 buy and sell before any shot, and at wave w's first
# entry a swap: sells its k oldest towers of the element that cannot touch wave w (they have fired) and rebuilds the other element
# at the next cells of the order. Then repaired so that every shift of 0..30 ticks (all actions after tick 0) has no refused action
# and still wins: the first action refused under any shift moves 30 ticks later, repeat.
import json, sys
from multiprocessing import Pool
from econ import *
from p4 import mkep
from proposal import *
class RefMaze(Strong):
    def __init__(self, w=8, k=2):
        super().__init__(); self.moved = False; self.sold = set(); self.w = w; self.k = k; self.name = f'reference (maze, reads, swaps {k} at wave {w})'
    def pick(self, b): return [c for c in super().pick(b) if c not in self.sold]
    def act(self, b):
        if b.tick_n == 0 and not self.log:
            b.place((0, 8), 'L'); self.log.append((0, (0, 8), 'buy', -b.cost, b.gold, 'L'))
            back = b.sell((0, 8)); self.log.append((0, (0, 8), 'sell', back, b.gold, 'L'))
        if not self.moved and b.waves.started >= self.w:
            self.moved = True; useless = wave_elem(b, self.w - 1)
            cells = [t.cell for t in b.towers if t.elem == useless][:self.k]
            for c in cells:
                back = b.sell(c); self.sold.add(c); self.log.append((b.tick_n, c, 'sell', back, b.gold, useless))
            for _ in cells: self.buy(b, self.pick(b), counter(useless))
        super().act(b)
def table_of(r): return [(t, c, a, e) for (t, c, a, _, _, e) in r['log']]
def shifted(a):
    table, s = a; r = run(PROP, Replay(table, s))
    return s, r['out'], r['lives'], r['end_tick'], r['gold'], r['nL'], r['nS'], r['first_kill'], r['bad']
def repair(table, pool, rounds=80):
    for k in range(rounds):
        res = pool.map(shifted, [(table, s) for s in range(31)])
        refused = [(x[8][0][0] - (x[0] if x[8][0][0] else 0), x[8][0][1]) for x in res if x[8]]
        lost = [x[0] for x in res if x[1] != 'Won']
        if not refused and not lost: return table, res
        if not refused: print('  loses under shifts', lost, 'with no refusal'); return table, res
        t0, c0 = min(refused)
        i = next(i for i, (t, c, a, e) in enumerate(table) if c == c0 and t <= t0 + 30 and t >= t0 - 30 and t > 0)
        t, c, a, e = table[i]
        # a later sale must stay after the purchase it undoes; move the whole action
        table = sorted(table[:i] + [(t + 30, c, a, e)] + table[i + 1:], key=lambda x: x[0])
    return table, res
if __name__ == '__main__':
    pool = Pool(17)
    w, k = int(sys.argv[1]), int(sys.argv[2])
    bot = RefMaze(w, k); bot.ep = mkep('board')
    r = run(PROP, bot); table = table_of(r)
    print(bot.name, 'raw:', r['out'], r['lives'], r['end_tick'], r['nL'], r['nS'])
    table, res = repair(table, pool)
    outs = sorted({(x[1], x[2], x[3], x[4], x[5], x[6], x[7]) for x in res})
    print('  repaired, over shifts 0..30 (outcome, lives, end tick, gold, L, S, first kill):', outs)
    print('  shifts with refusals:', [x[0] for x in res if x[8]])
    json.dump(table, open(f'ref4_w{w}_k{k}.json', 'w'))
