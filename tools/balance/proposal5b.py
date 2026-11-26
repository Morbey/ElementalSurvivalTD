# The step 5b proposal of docs/balance/0001-prototype-economy.md: the perk pool and the retuned waves. Everything else is step 4e / 5a.
from perks5b import *

AMOUNTS = dict(dmg=1.25, rate=1.10, rng=1.0, gold=3)        # Light damage x, Shadow damage x, fire rate x, range +, gold per kill +
POOL = pool(**AMOUNTS)
HEALTH = [100, 100, 180, 180, 360, 680, 800]               # waves 4-10, whole hits of 20: only waves 8, 9 and 10 change from 4e
MIX = [(pt, h) for (pt, _), h in zip(pr.MIX, HEALTH)]       # the 4e entry orders
SEED = 4                                                    # the reference seed (refrepair5b.py seeds 200: wins with 10 lives, takes all five perks, both damages twice)


def params(mix=None, perks=None, seed=None, **kw):
    """The 5b run: the 4e economy, the 5a XP and table, this pool and these waves. seed None: each job sets its own."""
    return with_perks(pr.params(mix=mix or MIX, **kw), perks or POOL, seed)


def with_health(hs, **kw): return params(mix=[(pt, h) for (pt, _), h in zip(pr.MIX, hs)], **kw)


PROP = params()
