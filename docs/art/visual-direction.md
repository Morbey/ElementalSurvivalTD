# Elemental TD – Visual direction

The single home of how the game looks and reads. Rules and numbers live in `docs/design/DECISIONS.md`; this file only says how they are shown.

**Units.** UI sizes are reference pixels of the panel (reference 960 x 600, scale with screen size, match 0.5). The browser canvas is 960 x 540 from 4c (UI scale 0.95; 960 x 600 at 1.0 before); at 1920 x 1080 the scale is 1.9. World sizes are in cells. Colours are sRGB hex; "α" is alpha.

**Three checks for every look:** it reads with twenty enemies and twenty towers on screen; it never relies on hue alone (value, size, shape or a word carries the same meaning); it is cheap on WebGL (one material asset per colour, pooled objects, no per-frame allocation).

## 1. Slice 4

### 1.1 Board and scenarios
Each scenario has its own look; element colours (1.2) are the same in all of them. Elements are saturated and sit at the value extremes; a board is desaturated and mid-value, so both a very light and a very dark element stand out on it.

**Scenario palette test.** Every scenario palette passes it before it is built. L is relative luminance, ratios are WCAG contrast, S is HSV saturation; all are targets on base colours, checked again on build screenshots.

| Surface | Must pass |
|---|---|
| Floor | L between 0.10 and 0.36 (contrast at least 2.0 with both the Light and the Shadow body); S at most 0.20, so Fire and Water, which share the floor's value, read by saturation; not green, red, orange or blue in hue |
| Wall | contrast at least 2.5 with the floor; S at most 0.25 |
| Spawn, exit | achromatic; contrast at least 2.5 with the floor and with each other |
| Background | contrast at least 3.0 with the floor; at least 7.0 with `#FFFFFF` (HUD text) |

**Tightest element colours**: Light caps how bright a floor may be (L 0.36) and Shadow how dark (L 0.10); Fire limits its hue, so no warm saturated floor such as lava or desert orange.

**Scenario 1, stone** (accepted for testing in 4b; Eduardo judges it in the build):

| Surface | Before | Stone | Test |
|---|---|---|---|
| Free cell (`Cell_Free`) | `#BFBFBF` | `#968F7F` | L 0.28, S 0.15: passes |
| Wall (`Cell_Blocked`) | `#333340` | `#4D4943` | 2.8: passes |
| Camera background (grid lines and the area around the board) | taupe from the clear | solid `#2E2C29` | 4.3 and 13.9: passes |
| Spawn, exit | `#33BF4D`, `#D94033` | the rings of section 3 | green and red fail (1.3 and 1.4 with the floor, read by hue only); the rings pass (2.9 and 5.7) |

### 1.2 Element colours
Accepted for testing; Eduardo judges them in the build. One row per base element, fixed now so later elements need no repaint. **Body** colours the tower and the enemy (URP Lit). **Projectile** is the Unlit sphere. **UI** is the element's name and accents on the dark HUD background. **Value** is the non-hue cue: elements that share a value tier differ on the blue–yellow axis, which colour-blind players keep.

| Element | Body | Projectile | UI | Value | Extra |
|---|---|---|---|---|---|
| **Light** | `#FFE27A` | `#FFF6CC` | `#FFE27A` | brightest | body emission `#5C4A12`: every face stays bright, even in shade; no halo or bloom wanted |
| **Shadow** | `#3B1F5C` | `#2A1340` | `#B48CFF` | darkest | |
| Air | `#BDF2EC` | `#FFFFFF` | `#BDF2EC` | bright, cool | |
| Fire | `#F06A2B` | `#FFB347` | `#FF8A4C` | mid, warm | reads by saturation against the stone; recheck when Fire is built |
| Water | `#1F5FC0` | `#7CC4FF` | `#6DB4FF` | mid-dark, cool | |
| Earth | `#7A5230` | `#4A2E14` | `#C9925A` | dark, warm | |

Hybrids get their own row when they are built (Nature green, Lightning, Hellfire, Poison); each must pass the scenario test of 1.1 like the base elements.

- Tower and enemy of the same element share the body colour; shape tells them apart (cube 0.7 cells against a flattened sphere 0.5 cells).
- Bodies are matte (smoothness 0): a specular highlight whitens Light into its own projectile colour.
- Projectile: sphere **0.25 cells** (was 0.2), Unlit, no shadow, at the height it has now.
- One material per colour (ADR 0002), for example `Tower_Light`, `Enemy_Shadow`, `Projectile_Light`.

### 1.3 HUD
Nothing permanent sits over the board. The camera frames the board inside the screen area between the top bar and the bottom bar, with **0.3 cells** of padding on every side (a cell is 50.4 px on the 960 x 600 canvas of 4b, 44.8 px with the side column of 4c). The bars, and from 4c the side column, have background `#2E2C29`, the camera background's colour: they look like no background, but the range disc never shows under their text. Check on a screenshot that the two colours match.

**Text style** (`.hud-text`): bold, `#FFFFFF`, no outline and no shadow (the background is dark).

**Top bar**: anchored top, full width, height **44**, padding 0 16, one row, centred vertically.
- Left: `Wave 3/10: Shadow` then, 24 to its right, `Lives 10`. Both 20. The element name is the current wave's (the wave last started), in its UI colour; before wave 1 the label is `Wave 0/10`.
- Right, right-aligned: `Next wave in 8 s: Shadow`, 20, element name in its UI colour; shown only while a countdown runs (hidden while a wave spawns and when no wave is left). Mixed waves later list names joined by ` + `, each in its colour, so the label grows to the left (room for three names at 960 wide).
- If several upcoming waves are announced (open with Eduardo), the right side becomes `Next in 8 s` (20) followed by one chip per upcoming wave, up to three, 8 apart, in wave order. Chip: a row of element tiles (below), padding 2, gap 2, background `#45413B`, radius 4; the next wave's chip has border 2 `#FFFFFF`, later chips opacity 0.7. Three chips of three elements take about 230, so the bar still fits at 960. The current wave stays as text on the left.
- **Element tile**, the reusable mark for an element in small UI: 20 x 20, the element's body colour, border 2 `#D9D4C8`, radius 3, its initial centred (14, bold; `#1A1918` on Light and Air, `#FFFFFF` on the others). Initials: L, S, F, W, E, A; a hybrid gets a letter that no other element uses when it is defined.
- Element names are coloured with rich text, for example `<color=#B48CFF>Shadow</color>`. A label's text is rebuilt only when its seconds or elements change.

**Bottom bar**: anchored bottom, full width, height **72**, padding 0 16, one row, centred vertically. Economy and building live here.
- Left: `Gold 115`, 24, `#FFC23D`.
- 16 to its right, the message line, 20, bold, one label shared by two kinds; a newer message replaces the one shown, and each fades to opacity 0 over 0.3 s (USS transition) at the end of its time:
  - wave bonus `Wave 3 survived +25`: `#FFC23D`, 3.0 s;
  - placement refusal, until it moves to the refused cell (below): `#FFFFFF`, 2.0 s, the texts of today.
- Right, right-aligned: the tower buttons, 12 apart.
- From 4c the tower buttons move to the side column (1.5), where six fit; the bottom bar keeps the gold and the message line.

**Tower button** (`.tower-button`), one per buyable element, in element order (Light, Shadow):
- 152 x 56, row, centred vertically, padding 0 10, background `#45413B`, border 2 `#6B655A`, radius 6.
- Children, left to right: key hint `1` (16, regular, `#B5AFA3`, width 14); swatch 22 x 22 in the colour of the element's tower material, border 2 `#D9D4C8` (the light rim keeps the dark Shadow swatch visible on the button), radius 3, margin 0 8, no letter while the name is on the button; a column with the name `Light` (18, bold, `#FFFFFF`) over the price `40 gold` (16, regular, `#FFC23D`).
- Hover and pressed: background `#57524A`. Not focusable: keys 1 and 2 are the keyboard path, so no focus look.
- Selected: background `#6B655A`, border 3 `#FFFFFF` (padding 0 9 so the size holds). Exactly one button is selected; keys 1 and 2 select too.
- Gold below the price: the price turns `#FF6B5E`. The button stays clickable, so the player can still select it.

**Placement refusal at the cell** (the damage-numbers step; until then it uses the message line): One pooled label, 18, bold, `#FFFFFF`, outline 1 `#000000`. Its bottom centre starts 0.6 cells above the cell centre (screen up), rises 16 over 1.5 s linear, and fades to 0 over the last 0.4 s; clamped 8 inside the board area. A new refusal restarts it. Texts are unchanged.

**End screen**: overlay `rgba(20, 19, 17, 0.8)` over everything. Title 48, bold: `Victory` in `#FFC23D`, `Defeat` in `#FF6B5E`. The countdown and the message line hide when it shows; the rest of the HUD stays under it. 24 below, the Restart button: padding 10 32, background `#45413B`, border 2 `#FFFFFF`, radius 6, text 24 bold `#FFFFFF`; hover background `#57524A`.

### 1.4 Hover and range
The hover is a flat quad on the cell, **under** the tower, so it never washes a tower out; under a tower only its rim shows.
- Quad 0.95 cells (the tile size), at height 0.02; draws after the range disc. Unlit, transparent, one material per state.

| Hovered cell | Hover | Range disc |
|---|---|---|
| Placing the selected tower would succeed | `#FFFFFF` α 0.30 | selected element's |
| Placing would be refused for a reason that ignores enemies: spawn or exit, wall, gold, or cutting the spawn from the exit (parked; until then, like a free cell) | `#E5483B` α 0.45 | hidden |
| A tower stands there | the same `#FFFFFF` α 0.30, read as a rim around the tower: it is clickable | that tower's |

No sale look on towers in 4b: a click still sells there, but 4c replaces it a step later, so a look built for it would be thrown away.

- Range disc: flat at height 0.01, radius = range. Fill is the element's UI colour at α 0.18 (`RangeDisc_Light` `#FFE27A`, `RangeDisc_Shadow` `#B48CFF`), so the disc also says which tower is about to be built.
- The hover state is recomputed only when the hovered cell, the selection, the gold or the board changes, not every frame. Refusals caused by enemies (an enemy on the cell, or a living enemy cut off) are not shown on the hover: they change every tick and would flicker; the click refuses them with its message.
- When the pointer leaves the canvas, the hover and disc should hide (parked). Acceptable until then: leaving through the top or the bottom passes over a bar, where they already hide; only a side exit leaves them on an edge column.

### 1.5 Tower selection (4c)
**Selection frame**: a square frame of four bars around the selected tower's cell, outer 1.1 cells, bar width 0.08 cells, at height 0.03, Unlit, opaque `#FFFFFF` (contrast 3.2 with the stone floor). It lies outside the tile, over the grid lines, with a ring of floor between it and the tower, so it never touches or tints the tower: Light and Shadow keep their contrast with the floor. It differs from the hover by being opaque, larger than the cell and a frame, not a fill. One pooled frame; no pulse.

**Side column** (from 4c). The panel must later hold four or five stats and a list of synergies, which a 72-high bar cannot. So 4c adds a column on the right, width **216**, between the top and the bottom bar, padding 12, no background; the camera frames the board in the area left of it. It holds the buy buttons while no tower is selected and the tower panel while one is. Nothing in it lies over a cell, so the input rule of 4b holds.
- Cost: at 960 wide a cell goes from 50.4 to 44.8 px, at 16:10 and 16:9 alike; only a wider canvas wins it back. A bottom bar tall enough for the panel would cost height on every screen; a panel beside the tower would cover cells and enemies and bring back the click rule.
- Buy buttons in the column: the tower button of 1.3, 192 x 56, stacked 8 apart from the top; six take 376 of the 429 inside the column at any 16:9 size (460 at 16:10). The compact form is no longer needed.

**Tower panel** (`#tower-panel`), stacked from the top of the column; the actions sit at its bottom:
- **Header**: element tile 28 x 28 (1.3) without its letter, like the tower button's swatch, because the name stands beside it; 8 to its right, `Light tower`, 18, bold, `#FFFFFF`; at the row's right, the close button: 28 x 28, no background or border, `×` 20 bold `#B5AFA3`, hover and pressed `#FFFFFF`. A click on it ends the selection. It is the visible way out: Escape may leave the browser's full screen instead of reaching the game, and a click on the ground does not say that it closes.
- **Stats**, 12 below: one row each, height 22, label left 16 regular `#B5AFA3`, value right 16 bold `#FFFFFF`. In 4c: `Element` (the name in its UI colour), `Damage` `10`, `Shots/s` `1.5`, `Range` `2.5`. Later rows (kind of shot, and so on) append below. Recommended for 4c: the values exist, it costs four labels, and the panel gets its real shape now.
- **Synergies**, 16 below: heading `Synergies`, 14 bold `#B5AFA3`, then rows of height 28, up to six. In 4c it holds one line, `Found by playing the scenarios`, 14 regular `#8A8478`, marking the empty place.
  - Known synergy: a 24 x 24 icon in its colours and its name, 16 bold `#FFFFFF`.
  - Not yet discovered, if shown: a 24 x 24 tile `#3A3733` with border 1 `#6B655A` and a `?` (14, bold, `#8A8478`), with no name.
- **Actions**, at the bottom of the column: Upgrade 80 x 56 and, 12 to its right, Sell taking the rest (100 x 56), so a three-digit line such as `+125 of 250` fits (about 80 of the 84 inside Sell's border and padding 0 6). Recheck when the first three-digit cost is built.
- **Upgrade**, not available yet: background `#3A3733`, border 2 `#4F4A43`, radius 6; `Upgrade` 18 bold over `Soon` 14 regular, both `#8A8478`. Takes no clicks and has no hover or pressed look, so it reads as planned, not broken.
- **Sell**: background `#45413B`, radius 6; hover and pressed `#57524A`. `Sell` 18 bold `#FFFFFF` over the value line. A click sells at once and ends the selection.
- Height: header, stats and actions keep their size; the synergies area takes what is left (flex-grow 1, flex-shrink 1, min-height 0, overflow hidden) and becomes a vertical scroll when its rows outgrow it. 4c needs about 262 of the 429; the full panel, 28 + 12 + 88 + 16 + 18 + 168 + 16 + 56 = 402, fits, with a fifth stat row by 5.

**Sell value**, always what a click pays now. The value line is live while the panel is open, hovered or not: it follows the tower's state, never a pointer event.

| Tower | Value line | Sell border |
|---|---|---|
| Has never fired: full cost | `+40`, 16 bold `#FFC23D` | 2 `#FFC23D` |
| Has fired: refund | `+20`, 16 bold `#C9A04A`, then ` of 40`, 14 regular `#B5AFA3` | 2 `#6B655A` |

The two looks differ by colour, by the gold border and by the ` of 40` tail, not only by the number.

**The drop**, when the tower fires its first shot while the panel is open: on the frame after, the line switches to the refund and holds the pop look for one frame: number `#FF6B5E` at scale 1.4 (about its centre), Sell border 2 `#FF6B5E`, no transition. Then the settle look: the scale eases to 1.0 over 0.25 s, ease-out; the number's and the border's colours ease to the refund look over 0.5 s after a delay of 0.25 s. 0.75 s in all, so a resting pointer still sees it happen. A text is built when the value polled from the game changes, never per frame.

**Ending the selection**: a click on empty ground ends it and does not buy (the next click buys); the panel's close button ends it; Escape ends it; a click on another tower selects that one; a key 1 or 2 ends it and selects that tower to buy; selling ends it. Waves do not end it.

**While a tower is selected**: the range disc shows the selected tower's range and stays there wherever the pointer is; the hover still shows on other cells, without a disc of its own. Ending the selection brings back the buy buttons, with the selected tower to buy unchanged.

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
3. The weak number is dimmed by colour, not opacity, so its outline stays solid; its size floor is 16 (15 px on the browser canvas).
4. Weak numbers are the first to go under load: they carry the least decision.

## 3. Review of what is on screen
| Item | Verdict | Reason |
|---|---|---|
| Free cell, wall, background | change in slice 4 (1.1) | Light disappears on light grey; walls and grid lines share one dark |
| Tile 0.95 cells with gaps | keep | the gaps draw a 2 px grid for free |
| Spawn and exit | change (parked): spawn a ring `#F4F1EA` and exit a ring `#151413`, each 0.95 cells with a 0.55-cell inset in the free-cell colour | green and red fail the scenario test on stone and will mean Nature and Fire; achromatic rings tell in from out by value, and the inset keeps every enemy readable on them |
| Wall height | change, low priority: blocks 0.5 cells high on a full 1.0-cell footprint | walls cast shadows and join into one wall, the Bad North read of terrain |
| Tower cube 0.7, Lit, casts shadows | keep shape; colour per element (1.2) | |
| Enemy flattened sphere 0.5 x 0.25 | keep shape; colour per element (1.2) | |
| Directional light, real-time shadows | keep, with shadow strength 0.5; only towers cast shadows | shadows give the cubes their volume; at full strength a Shadow enemy disappears in a tower's shadow, and enemy shadows read as dark blobs |
| Health bar 0.8 x 0.12 cells, fill `#33D940`, back `#1A1A1A` | keep colours; hide the bar until the enemy first takes damage | twenty full bars are noise; a bar appears where something is happening |
| Projectile pink `#FF33E6`, 0.2 cells | change (1.2) | the colour said nothing; the element is the point |
| Hover white α 0.5 above the tower | change (1.4) | it washed the tower out |
| Range disc light blue α 0.15 | change (1.4); later (parked) a 0.05-cell edge line at α 0.6, a round mesh of 64 sides and the disc clipped to the board (the bars' and the column's background already hide it under the HUD) | the edge shows the exact reach on a busy board; the built-in cylinder shows its 20 facets, and the part beyond the board means nothing |
| Placement message, top centre over the board | change (1.3) | covered the top rows, far from where the player looked |
| HUD text with outline and shadow, stacked top-left | change (1.3) | covered the top-left cells |
| End screen and default Restart button | change (1.3) | the title said nothing by colour; the button looked like an editor control |

## 4. Open choices for Eduardo
1. **Spawn and exit as white and black rings in 4b, with the stone board,** instead of when green or red elements arrive (section 3). Recommended: green and red fail the scenario test on stone and Eduardo judges the stone board in that build; the cost is two materials and an inset quad on two cells. The alternative keeps green and red, read by hue only, until then.
