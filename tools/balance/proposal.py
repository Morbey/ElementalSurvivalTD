# The proposal of docs/balance/0001-prototype-economy.md (slice 4: Light and Shadow, wave bonus).
from p4 import params4
HP = [45, 55, 70, 85, 105, 135, 180, 245, 345, 460]
ORDER = 'SLLSLSSLSS'                       # enemy element per wave, 1 to 10
BONUS = [40, 20, 25, 30, 35, 40, 45, 50, 55, 60]      # survival bonus per wave (wave 1 pays the first counter tower); the last pays on the winning tick
S, C, G, REFUND = 115, 40, 10, 50
def up(hp, m, first=6): return [int(round(h * (m if i >= first else 1.0) / 5)) * 5 for i, h in enumerate(hp)]
def params(hp=HP, order=ORDER, bonus=BONUS, S=S, C=C, g=G, refund_pct=REFUND, **kw):
    return params4(hp, order, bonus, S=S, C=C, g=g, refund_pct=refund_pct, **kw)
PROP = params()
