# Slice 4 parameter builder and a quick scorer used by the searches.
from econ import *
COUNTS = list(range(3, 13))
def params4(hp, order, bonus=0, S=115, C=40, g=10, refund_pct=50, lives=10, counts=COUNTS, delays=None):
    delays = delays or [15] + [8] * 9
    bonus = bonus if isinstance(bonus, (list, tuple)) else [bonus] * 9 + [0]
    return dict(P, lives=lives, start_gold=S, cost=C, refund_pct=refund_pct,
                waves=[(counts[i], 1, delays[i], hp[i], g, order[i], bonus[i]) for i in range(10)])
def mk(kind, arg=0):
    return {'none': Nothing, 'sensible': Sensible, 'maze': Strong, 'far': Far, 'late': lambda: Late(arg),
            'saver': lambda: Hoarder(arg), 'swap': lambda: Swapper(*arg), 'sloppy': lambda: Sloppy(arg)}[kind]()
def mkep(e, arg=0):
    return {'reads': Reads, 'board': ReadsBoard, 'share': ReadsShare, 'ahead': lambda: ReadsAhead(arg), 'L': lambda: Mono('L'), 'S': lambda: Mono('S'), 'random': lambda: Random(arg),
            'halfL': lambda: Half('L'), 'halfS': lambda: Half('S'), 'none': NoElem,
            'thenL': lambda: ReadsThenMono('L', arg or 4), 'thenS': lambda: ReadsThenMono('S', arg or 4), 'skewL': lambda: Skew('L', arg), 'skewS': lambda: Skew('S', arg)}[e]()
def one(a):
    p, kind, arg, e, earg = a; return run(p, mk(kind, arg), ep=mkep(e, earg))
def short(r): return f"{r['out'][0]}{r['lives']}"
