# Elemental TD – Visual direction

The single home of how the game looks and reads. Rules and numbers live in `docs/design/DECISIONS.md`; this file only says how they are shown.

**Units.** UI sizes are reference pixels of the panel (960 x 600, scale with screen size, match 0.5): multiply by about 0.78 on the 750 x 469 browser canvas and 1.9 at 1920 x 1080. World sizes are in cells. Colours are sRGB hex; "α" is alpha.

**Three checks for every look:** it reads with twenty enemies and twenty towers on screen; it never relies on hue alone (value, size, shape or a word carries the same meaning); it is cheap on WebGL (one material asset per colour, pooled objects, no per-frame allocation).

## 1. Slice 4

### 1.1 Board
Elements are saturated and sit at the value extremes; the board is desaturated and mid-value, so both a very light and a very dark element stand out on it.

| Surface | Now | Slice 4 |
|---|---|---|
| Free cell (`Cell_Free`) | `#BFBFBF` | `#968F7F` warm stone |
| Wall (`Cell_Blocked`) | `#333340` | `#5E5850` |
| Camera background (grid lines and the area around the board) | taupe from the clear | solid colour `#2E2C29` |
| Spawn, exit | `#33BF4D`, `#D94033` | keep (see section 3) |

Contrast with the new board: Light body 2.5 against the free cell and 5.5 against the wall; Shadow body 4.3 and 1.9. Shadow never stands on a wall; only its projectile crosses one.

### 1.2 Element colours
One row per base element, fixed now so later elements need no repaint. **Body** colours the tower and the enemy (URP Lit). **Projectile** is the Unlit sphere. **UI** is the element's name and accents on the dark HUD background. **Value** is the non-hue cue: elements that share a value tier differ on the blue–yellow axis, which colour-blind players keep.

| Element | Body | Projectile | UI | Value | Extra |
|---|---|---|---|---|---|
| **Light** | `#FFE27A` | `#FFF6CC` | `#FFE27A` | brightest | body emission `#5C4A12`: it glows |
| **Shadow** | `#3B1F5C` | `#2A1340` | `#B48CFF` | darkest | smoothness 0: matte |
| Air | `#BDF2EC` | `#FFFFFF` | `#BDF2EC` | bright, cool | |
| Fire | `#F06A2B` | `#FFB347` | `#FF8A4C` | mid, warm | reads by saturation against the stone; recheck when Fire is built |
| Water | `#1F5FC0` | `#7CC4FF` | `#6DB4FF` | mid-dark, cool | |
| Earth | `#7A5230` | `#4A2E14` | `#C9925A` | dark, warm | |

Hybrids get their own row when they are built (Nature green, Lightning, Hellfire, Poison); green and red then force the spawn and exit change in section 3.

- Tower and enemy of the same element share the body colour; shape tells them apart (cube 0.7 cells against a flattened sphere 0.5 cells).
- Projectile: sphere **0.25 cells** (was 0.2), Unlit, no shadow, at the height it has now.
- One material per colour (ADR 0002), for example `Tower_Light`, `Enemy_Shadow`, `Projectile_Light`.

### 1.3 HUD
Nothing permanent sits over the board. The camera frames the board inside the screen area between the top bar and the bottom bar, with **0.3 cells** of padding on every side (about 39 px per cell on the browser canvas, against 42 now). The bars have no background: they sit on the camera background.

**Text style** (`.hud-text`): bold, `#FFFFFF`, no outline and no shadow (the background is dark).

**Top bar**: anchored top, full width, height **44**, padding 0 16, one row, centred vertically.
- Left: `Wave 3/10: Shadow` then, 24 to its right, `Lives 10`. Both 20. The element name is the current wave's (the wave last started), in its UI colour; before wave 1 the label is `Wave 0/10`.
- Right, right-aligned: `Next wave in 8 s: Shadow`, 20, element name in its UI colour; hidden when no wave is left. Mixed waves later list names joined by ` + `, each in its colour, so the label grows to the left (room for three names at 960 wide).
- Element names are coloured with rich text, for example `<color=#B48CFF>Shadow</color>`. A label's text is rebuilt only when its seconds or elements change.

**Bottom bar**: anchored bottom, full width, height **72**, padding 0 16, one row, centred vertically. Economy and building live here.
- Left: `Gold 115`, 24, `#FFC23D`.
- 16 to its right, the wave bonus `Wave 3 survived +25`, 20, `#FFC23D`; shown for 3.0 s, then opacity to 0 over 0.3 s (USS transition). A new bonus replaces it.
- Right, right-aligned: the tower buttons, 12 apart.

**Tower button** (`.tower-button`), one per buyable element, in element order (Light, Shadow):
- 152 x 56, row, centred vertically, padding 0 10, background `#45413B`, border 2 `#6B655A`, radius 6.
- Children, left to right: key hint `1` (16, regular, `#B5AFA3`, width 14); swatch 22 x 22 in the element's body colour, border 1 `#1A1918`, radius 3, margin 0 8; a column with the name `Light` (18, bold, `#FFFFFF`) over the price `40 gold` (16, regular, `#FFC23D`).
- Hover: background `#57524A`.
- Selected: background `#6B655A`, border 3 `#FFFFFF` (padding 0 9 so the size holds). Exactly one button is selected; keys 1 and 2 select too.
- Gold below the price: the price turns `#FF6B5E`. The button stays clickable, so the player can still select it.

**Placement refusal**: shown at the refused cell instead of over the top rows. One pooled label, 18, bold, `#FFFFFF`, outline 1 `#000000`. Its bottom centre starts 0.6 cells above the cell centre (screen up), rises 16 over 1.5 s linear, and fades to 0 over the last 0.4 s; clamped 8 inside the board area. A new refusal restarts it. Texts are unchanged.

**End screen**: overlay `rgba(20, 19, 17, 0.8)` over everything. Title 48, bold: `Victory` in `#FFC23D`, `Defeat` in `#FF6B5E`. 24 below, the Restart button: padding 10 32, background `#45413B`, border 2 `#FFFFFF`, radius 6, text 24 bold `#FFFFFF`; hover background `#57524A`.

### 1.4 Hover and range
The hover is a flat quad on the cell, **under** the tower, so it never washes a tower out; under a tower only its rim shows.
- Quad 0.95 cells (the tile size), at height 0.02; draws after the range disc. Unlit, transparent, one material per state.

| Hovered cell | Hover | Range disc |
|---|---|---|
| Placing the selected tower would succeed | `#FFFFFF` α 0.30 | selected element's |
| Placing would be refused, for any reason including gold | `#E5483B` α 0.45 | hidden |
| A tower stands there (a click sells it) | `#FFC23D` α 0.60, read as a gold rim: money comes back | that tower's |

- Range disc: flat at height 0.01, radius = range. Fill is the element's UI colour at α 0.18 (`RangeDisc_Light` `#FFE27A`, `RangeDisc_Shadow` `#B48CFF`), so the disc also says which tower is about to be built.
- The hover state is recomputed only when the hovered cell, the selection, the gold or the board changes, not every frame. When the pointer leaves the canvas, hover and disc hide.

## 2. Damage numbers
The closed look, in values. Numbers are pooled; nothing allocates per hit.

| Kind | Text | Size | Colour | Life |
|---|---|---|---|---|
| Weak | `2` | 16 regular | `#B3ACA0` | 0.5 s |
| Normal | `8` | 20 bold | `#FFFFFF` | 0.7 s |
| Strong | `16` | 26 bold | `#FFC23D` | 0.8 s |
| Critical | the kind's number + `!` | its kind's size x 1.3 (21, 26, 34) | its kind's colour | its kind's + 0.2 s |
| Immune | `immune` | 16 regular | `#BDBDBD` | see below |

- All kinds: outline 1 `#000000`, no shadow. Whole numbers, rounded to nearest, at least 1.
- **Position**: spawned 0.7 cells above the enemy centre (screen up, just above the health bar), at that point in the world; it does not follow the enemy.
- **Motion**: rises 20 with ease-out over its life; full opacity for the first 60 %, then linear to 0.
- **Pop (critical)**: scale 1.6 to 1.0 over 0.12 s, ease-out, then the normal motion.
- **No stacking**: each new number takes the next spawn offset (x, y) from the cycle (0, 0), (-12, -6), (12, -6), (-6, -12), (6, -12), one global counter.
- **Cap**: 40 numbers alive; at the cap the oldest is recycled. From 30 alive, weak numbers are not spawned.
- **Immune**: at most one per enemy. It follows the enemy at the same 0.7-cell offset, appears without a pop, and hides 1.0 s after the last immune hit on that enemy (fade over the last 0.2 s). Towers fire every 0.67 s, so a steady stream keeps it alive. It does not count towards the cap.

**Changes to the closed look**, each a clarification it left open:
1. A critical keeps the colour and the relative size of its kind (a strong critical is a large gold `32!`), so the "!" never hides whether the hit was strong or weak.
2. The immune word is per enemy, not per tower and enemy: three towers wasting shots on one enemy show one word, not three.
3. The weak number is dimmed by colour, not opacity, so its outline stays solid; its size floor is 16 (12.5 px on the browser canvas).
4. Weak numbers are the first to go under load: they carry the least decision.

## 3. Review of what is on screen
| Item | Verdict | Reason |
|---|---|---|
| Free cell, wall, background | change in slice 4 (1.1) | Light disappears on light grey; walls and grid lines share one dark |
| Tile 0.95 cells with gaps | keep | the gaps draw a 2 px grid for free |
| Spawn and exit | keep until the first green or red element; then spawn a ring `#F4F1EA` and exit a ring `#151413`, each 0.95 cells with a 0.55-cell inset in the free-cell colour | green and red will mean Nature and Fire; achromatic rings tell in from out by value, and the inset keeps every enemy readable on them |
| Wall height | change, low priority: blocks 0.5 cells high on a full 1.0-cell footprint | walls cast shadows and join into one wall, the Bad North read of terrain |
| Tower cube 0.7, Lit, casts shadows | keep shape; colour per element (1.2) | |
| Enemy flattened sphere 0.5 x 0.25 | keep shape; colour per element (1.2) | |
| Directional light, real-time shadows | keep | shadows give the cubes their volume from straight above |
| Health bar 0.8 x 0.12 cells, fill `#33D940`, back `#1A1A1A` | keep colours; hide the bar until the enemy first takes damage | twenty full bars are noise; a bar appears where something is happening |
| Projectile pink `#FF33E6`, 0.2 cells | change (1.2) | the colour said nothing; the element is the point |
| Hover white α 0.5 above the tower | change (1.4) | it washed the tower out |
| Range disc light blue α 0.15 | change (1.4); later a 0.05-cell edge line at α 0.6 | the edge shows the exact reach on a busy board |
| Placement message, top centre over the board | change (1.3) | covered the top rows, far from where the player looked |
| HUD text with outline and shadow, stacked top-left | change (1.3) | covered the top-left cells |
| End screen and default Restart button | change (1.3) | the title said nothing by colour; the button looked like an editor control |

## 4. Open choices for Eduardo
1. **Board in warm mid stone (`#968F7F`) instead of light grey.** Recommended: it is what lets Light and Shadow both read. The alternative, a grass green ground in the Bad North way, collides with Nature and Poison.
2. **Spawn and exit as white and black rings once green or red elements exist** (section 3). Recommended; until then the green and red cells stay.
