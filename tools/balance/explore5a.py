# Step 5a design search for the level table (docs/balance/0001, part "Step 5a"). The tables of the proposal: report5a.py.
# python3 explore5a.py tables | robust
# tables: every level table with a first step of 3 kills and strictly rising steps (in kills of 10 XP) that gives 6 to 8 level-ups in
#         75 kills, scored on the reference order and every run of 72+ kills (fixed strategies and won crowd runs).
# robust: the best candidates and the proposal on stronger and weaker towers (health -2, -1, +1 hit in waves 4-10, a proxy for 5b perks)
#         and 5 s between waves.
import sys
from xp5a import *
from report4e import params, MIX

VARIANTS = [('Proposal', pr.PROP), ('Health -2 hits (stronger towers)', params(mix=[(p_, h - 40) for p_, h in MIX])),
            ('Health -1 hit', params(mix=[(p_, h - 20) for p_, h in MIX])), ('Health +1 hit', params(mix=[(p_, h + 20) for p_, h in MIX])),
            ('5 s between waves', params(delays=[15] + [5] * 9))]


def full_runs(fx, cr): return [r for _, r in fx if r['kills'] >= 72] + [r for rs in cr.values() for r in rs if r['out'] == 'Won']


def all_tables():
    out = []
    def rec(seq, total):
        if total > 75:
            if 6 <= len(seq) - 1 <= 8: out.append(seq)
            return
        for nx in range(seq[-1] + 1, 30):
            rec(seq + [nx], total + nx)
            if nx + total > 75: break   # the first step past 75 kills is never reached: keep one
    rec([3], 3); return out


def score(runs, levels):
    s = [targets(r, levels) for r in runs]; return sum(a and b and c for a, b, c, _, _ in s) / len(s)


def rough(seq):
    d = [b - a for a, b in zip(seq, seq[1:])]; return max(d) - min(d), sum(abs(b - a) for a, b in zip(d, d[1:]))


if __name__ == '__main__':
    pool = Pool(17); mode = sys.argv[1] if len(sys.argv) > 1 else 'tables'
    if mode == 'tables':
        fx, cr = play(pool); ref = dict(fx)['Reference order']; runs = full_runs(fx, cr)
        ok = []
        for seq in all_tables():
            L = [10 * k for k in seq]; a = targets(ref, L)
            if a[0] and a[1] and a[2] and score(runs, L) == 1.0: ok.append((seq, a[3], a[4]))
        print(f'{len(ok)} tables meet all three targets on the reference order and on all {len(runs)} runs of 72+ kills')
        for n in (6, 7, 8):
            c = sorted([o for o in ok if o[1] == n], key=lambda o: rough(o[0][:n + 1]))
            print(f'{n} level-ups: {len(c)} tables; smoothest (steps in kills, per-window level-ups on the reference order):')
            for o in c[:6]: print('  ', o[0][:n + 1], o[2])
    if mode == 'robust':
        cands = {'proposal (7 ups)': LEVELS, '6 ups [3,4,7,10,14,17,21]': [30, 40, 70, 100, 140, 170, 210, 250],
                 '7 ups [3,4,7,10,12,14,16]': [30, 40, 70, 100, 120, 140, 160, 180], '7 ups [3,4,7,9,11,13,14]': [30, 40, 70, 90, 110, 130, 140, 150],
                 '8 ups [3,4,7,9,10,11,13,16]': [30, 40, 70, 90, 100, 110, 130, 160, 170]}
        print('| Variant | runs of 72+ kills | ' + ' | '.join(cands) + ' |')
        for vn, p in VARIANTS:
            fx, cr = play(pool, with_xp(p)); runs = full_runs(fx, cr)
            print(f'| {vn} | {len(runs)} | ' + ' | '.join(f'{100 * score(runs, L):.0f}%' for L in cands.values()) + ' |', flush=True)
