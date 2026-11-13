# Prints every table of step 5a (XP and level, counters only). Run verify3c.py, verify4b.py and verify4e.py first for the port pins.
# python3 report5a.py main | xp
# main: the table, the pins (building nothing, the reference order), every 4e strategy and crowd.
# xp:   kills only against a wave XP bonus and against XP rising by wave, on the gap between strong and weak players.
import sys, statistics
from xp5a import *
from report4e import REF

WAVES = [3, 4, 5, 6, 7, 8, 9, 10, 11, 12]


def med(xs): xs = [x for x in xs if x is not None]; return statistics.median(xs) if xs else None
def fmt(x): return '–' if x is None else (f'{x:g}' if isinstance(x, float) else str(x))


def gaps(rs, ref_lv, waves=(3, 5, 7, 9), lu_of=None):
    """Mean levels behind the reference order at the end of the given waves, among the runs still alive there."""
    out = []
    for w in waves:
        d = [ref_lv[w - 1] - level_at_ends(lu_of(r), r)[w - 1] for r in rs if level_at_ends(lu_of(r), r)[w - 1] is not None]
        out.append(f'{sum(d) / len(d):.1f}' if d else '–')
    return out


if __name__ == '__main__':
    pool = Pool(17); mode = sys.argv[1] if len(sys.argv) > 1 else 'main'
    fx, cr = play(pool); ref = dict(fx)['Reference order']
    port_lu = lambda r: r['levelup_ticks']
    for _, r in fx + [x for v in cr.values() for x in zip([None] * len(v), v)]:
        assert r['levelup_ticks'] == levelups(events_of(r)) and r['xp'] == XP * r['kills'] and r['level'] == 1 + len(r['levelup_ticks'])
    if mode == 'main':
        print(f'XP per enemy {XP}; a run that kills all 75 earns {75 * XP}. Level table (the n-th item takes level n to n + 1):')
        print('| Level | XP to the next | XP total to reach the next | Kills | Reached in a 75-kill run |')
        for n, (x, c) in enumerate(zip(LEVELS, cum(LEVELS)), 1): print(f'| {n} → {n + 1} | {x} | {c} | {c // XP} | {"yes" if c <= 75 * XP else "no"} |')
        print(f'level-ups in a 75-kill run: {sum(c <= 75 * XP for c in cum(LEVELS))}; highest level the table allows: {len(LEVELS) + 1}')
        b = Battle(mk_waves(PROP5), PROP5['lives'], TS, gold=PROP5['start_gold'], cost=PROP5['cost'], refund_pct=PROP5['refund_pct'], levels=LEVELS)
        while b.outcome == 'Running': b.tick()
        print(f'pin, builds nothing: {b.outcome} on tick {b.tick_n}, XP {b.xp}, level {b.level}, level-ups {b.levelup_ticks}')
        print(f"pin, reference order: {ref['out']} on tick {ref['end_tick']}, {ref['kills']} kills, XP {ref['xp']}, level {ref['level']}, level-up ticks {ref['levelup_ticks']}")
        acts = {t for t, *_ in REF}; kt = [t for t, w in ref['kill_log']]; entry = entries(ref)
        print('| Level reached | Tick | Time s | Kill number | Enemy of wave | Wave on the HUD (window) | XP after | Same tick as a wave bonus | A reference action on that tick |')
        for i, t in enumerate(ref['levelup_ticks']):
            k = sum(1 for x in kt if x <= t); w = ref['kill_log'][k - 1][1] + 1
            bon = [j + 1 for j, p in enumerate(ref['paid_tick']) if p == t]
            print(f"| {i + 2} | {t} | {t / 60:.1f} | {k} | {w} | {wave_of_tick(t, entry)} | {k * XP} | {'wave ' + str(bon[0]) if bon else '–'} | {'yes' if t in acts else 'no'} |")
        print('reference: level at the end of each wave window', level_at_ends(ref['levelup_ticks'], ref), 'level-ups per window', per_wave(ref['levelup_ticks'], entry))
        ref_lv = level_at_ends(ref['levelup_ticks'], ref)
        print('| Strategy | Outcome | End tick | Kills | XP | Final level | Level at the end of waves 1–10 | Level-ups per wave window | Pacing targets met |')
        for n, r in fx:
            t = targets(r)
            print(f"| {n} | {r['out']} | {r['end_tick']} | {r['kills']} | {r['xp']} | {r['level']} | {' '.join(fmt(x) for x in level_at_ends(r['levelup_ticks'], r))} | "
                  f"{' '.join(fmt(x) for x in t[4])} | {'yes' if all(t[:3]) else 'no: ' + ', '.join(s for s, ok in zip(('first', 'two in a wave', 'two waves without'), t[:3]) if not ok)} |")
        print('| Crowd (40 seeds) | Won | Kills, median (range) | Final level, median (range) | Median level at the end of waves 1–10 (runs alive) | Levels behind the reference at the end of waves 3 / 5 / 7 / 9 | Runs of 72+ kills meeting the pacing targets |')
        for n, rs in cr.items():
            kl = [r['kills'] for r in rs]; lv = [r['level'] for r in rs]; fr = [r for r in rs if r['kills'] >= 72]
            ok = sum(all(targets(r)[:3]) for r in fr)
            ends = [med([level_at_ends(r['levelup_ticks'], r)[k] for r in rs]) for k in range(10)]
            print(f"| {n} | {sum(r['out'] == 'Won' for r in rs)} | {med(kl):g} ({min(kl)}–{max(kl)}) | {med(lv):g} ({min(lv)}–{max(lv)}) | "
                  f"{' '.join(fmt(x) for x in ends)} | {' / '.join(gaps(rs, ref_lv, lu_of=port_lu))} | {ok}/{len(fr)} |")
        print('| Strategy | Levels behind the reference at the end of waves 3 / 5 / 7 / 9 |')
        for n, r in fx: print(f"| {n} | {' / '.join(gaps([r], ref_lv, lu_of=port_lu))} |")
    if mode == 'xp':
        # same 750 XP for a run that kills everything, so the same table applies
        schemes = [('Kills only: 10 per enemy (proposal)', XP, None),
                   ('Kills 7 per enemy + a wave XP bonus of 3 per enemy of the wave, paid with the gold bonus', 7, [3 * n for n in WAVES]),
                   ('XP rising by wave: 6 per enemy in wave 1 up to 13 in wave 10 (same table)', [6, 7, 8, 8, 9, 10, 11, 12, 12, 13], None)]
        for name, xp, wx in schemes:
            tot = sum((xp[k] if isinstance(xp, list) else xp) * n for k, n in enumerate(WAVES)) + sum(wx or [])
            lu_of = lambda r, xp=xp, wx=wx: levelups(events_of(r, xp, wx))
            ref_lv = level_at_ends(lu_of(ref), ref)
            print(f'{name}: a full run earns {tot} XP; reference level at the end of waves 1–10 {ref_lv}')
            print('| Crowd or strategy | Final level, median (range) | Levels behind the reference at the end of waves 3 / 5 / 7 / 9 |')
            for n, rs in cr.items():
                lv = [1 + len(lu_of(r)) for r in rs]
                print(f"| {n} | {med(lv):g} ({min(lv)}–{max(lv)}) | {' / '.join(gaps(rs, ref_lv, lu_of=lu_of))} |")
            for n, r in fx:
                if n in ('Late: first tower at wave 2, reads', 'Saver: 2 at start, then batches of 3, reads', 'Light only', 'Shadow only'):
                    print(f"| {n} | {1 + len(lu_of(r))} | {' / '.join(gaps([r], ref_lv, lu_of=lu_of))} |")
