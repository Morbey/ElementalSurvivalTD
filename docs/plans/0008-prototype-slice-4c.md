# Technical plan 0008 – Prototype slice 4, step 4c

Selecting a tower: its panel, Upgrade (shown, not working) and Sell with a live value. Architecture: `docs/adr/0004-run-waves-outcome-restart.md`, amended for the sell value, and `docs/adr/0002-presentation-layer-ui-toolkit.md`. Looks: `docs/art/visual-direction.md` ("the visual direction"), section 1.5. Numbers do not change: `docs/balance/0001-prototype-economy.md` stays as it is. Prototype mode: this plan carries the acceptance criteria. It builds on plan 0007 and reuses its "Editor sequencing".

**What Eduardo sees:** a column on the right with the buy buttons. A click on a tower frames it, keeps its range on the board and swaps the buttons for a panel that says what the tower is and what selling pays; the price drops in front of him when the tower fires its first shot, and Sell pays what it shows.

## Approach
**The sell value is one rule, asked two ways.** `Battle` gains a query for what a sale of the tower on a cell pays now, and `TrySellTower` pays through it, so the refund is written once (ADR 0004, decision 3). The panel reads the query every frame in `LateUpdate`, after the frame's ticks, and rewrites its texts only when the number changes. Its look follows the amount: the full look while the value equals the cost, the refund look below it; a value that falls for the tower already shown is the drop. `Tower.HasFired` stays internal.

**The selection is input state.** `TowerPlacementInput` already holds the tower to buy; it now also holds the tower selected on the board, as a reference to the Core `Tower`, and answers null once that tower is sold or the run has ended. Core knows nothing of it, and a sale still goes through `Battle.TrySellTower`. A click outside the grid changes nothing: the press of a click on Sell reaches the board input before the button's own click does, so ending the selection there would cancel the sale.

**The column is layout only.** `Hud.uxml` puts `board-area` and a new `side-column` in one row between the bars. The camera framer does not change: it already fits the board into `board-area` wherever that lies (ADR 0002, decision 5). One class on the column says whether it shows the buy buttons or the panel, so `TowerBarView` does not change either. A new `TowerPanelView` fills the panel when the selection changes. Every time of the drop lives in `Hud.uss`: the view only switches classes on the frames the value changes.

Discarded: the selection in Core (it changes nothing in the simulation); a public `Tower.HasFired` read by the view (two facts that must agree; the amount alone says which look applies); the refund computed in the view, or its text built before the tower fires (the view would need the rule); a Core event for the first shot (ADR 0003: polled); a new input component for the selection (the click that buys, selects or ends it is one decision); the drop animated from code (its times and curve would leave the style sheet); a general UI check before board clicks (ADR 0004: still no element that takes clicks over a cell); a panel that resizes the board area (the board would jump on every selection).

## Acceptance criteria
- **4c-AC1 Sell value.** `Battle` answers what a sale of the tower on a cell would pay now, and changes nothing by answering: the tower's cost while it has never launched a shot, then that cost times the refund percent, divided by 100 and rounded down (3b-AC5). It answers false for a cell with no tower or off the grid. A sale pays exactly the value the query gives on the same tick.
- **4c-AC2 Selecting.** A click on a cell with a tower selects that tower and neither sells nor buys; a click on another tower selects that one, and a click on the selected tower keeps it. At most one tower is selected, none at the start of a run. This replaces 2b-AC2's click, the click of 3b-AC5 and the selling of 4b-AC6.
- **4c-AC3 Ending.** The selection ends with a click on the panel's close button, with Escape, with a click on a cell that has no tower (which buys nothing and shows no refusal; the next click buys, 3b-AC2), with a sale, and when a tower to buy is chosen by its number key or its button, also the one already chosen. A click outside the grid, a key outside the list and a wave change nothing. Selecting a tower and ending the selection otherwise leave the tower to buy as it was.
- **4c-AC4 Sell and Upgrade.** A click on Sell sells the selected tower through `Battle.TrySellTower` and ends the selection; nothing else sells. Upgrade takes no click and no hover look, and changes nothing.
- **4c-AC5 After the end.** Once the run is won or lost no tower is selected and no click selects one: an open panel, the frame and the disc are gone from the frame after. 3b-AC6 holds.
- **4c-AC6 On the board.** While a tower is selected, a frame lies around its cell and the range disc lies on it with its range and its element's material, wherever the pointer is, also off the grid and over the UI. The hover shows on the hovered cell as in 4b-AC17 and no longer moves the disc. With no tower selected 4b-AC17 holds. All from the frame after a change. The frame, its material and height are those of the visual direction, section 1.5 ("Selection frame", "While a tower is selected").
- **4c-AC7 Side column.** A column between the bars holds the buy buttons while no tower is selected and the tower panel while one is, never both. 4b-AC7 holds for the buttons in their new place; the bottom bar keeps the gold and the message line. The camera frames the board in the area left of the column (4b-AC16), at any aspect and after a resize, so nothing in the column lies over a cell, and 4b-AC8 holds for the column. The column, the buttons in it and what the bars keep are those of the visual direction, sections 1.3 and 1.5 ("Side column").
- **4c-AC8 Panel.** The panel shows, for the selected tower: a tile in the colour of its element's tower material and a title with the element's display name; the rows Element (the display name in the element's UI colour), Damage, Shots/s and Range, read from that tower's Core stats and written with a point as the decimal sign on every machine; the Synergies heading with its empty line; Upgrade and Sell. These texts are built only when the selected tower changes. The wording, the sizes and the styles are those of the visual direction, section 1.5 ("Tower panel", "Upgrade", "Sell"), except the tile's letter (see "Not in this plan").
- **4c-AC9 Live sell value.** While the panel is open, every frame drawn shows on Sell the value of 4c-AC1 for the selected tower as it is after the last tick run before that frame; no code outside Core computes a refund. Sell has the full look while the value equals the tower's cost and the refund look, with the cost after it, while it is below. When the value changes for the tower already shown, the number and the look change on the first frame drawn after that tick and the drop plays, whether the pointer moves, rests on Sell or is elsewhere. A tower selected after it has fired shows the refund look with no drop. A click on Sell pays what Core answers at that moment. The texts, both looks and the drop are those of the visual direction, section 1.5 ("Sell value", "The drop").
- **4c-AC10 Canvas.** The WebGL player's canvas is 960 x 540.
- **4c-AC11 Layers and allocation.** The selection lives in `ElementalTD.Gameplay`; `Tick()` and its order do not change, and 3b-AC10 and 4b-AC13 hold. With a tower selected and the panel open, the input, the board view and the panel view allocate 0 bytes per frame after warm-up; the query allocates 0 bytes; a text is built only when what it shows changes.
- **4c-AC12 WebGL and regression.** All of the above works in the worktree build at `http://localhost:8766`, in Chrome and Safari. Everything of 2b to 4b that this plan does not replace still holds.

## What this step takes from the visual direction
The assets task applies these values; the code tasks build what holds them.

| Section of the direction | Where the values live |
|---|---|
| 1.5 selection frame | `SelectionFrame.prefab`, `SelectionFrame.mat` |
| 1.5 side column; 1.3 tower button, in its column size and spacing | `Hud.uss` |
| 1.5 tower panel, Upgrade, Sell, both sell looks, the drop with its times | `Hud.uss` |
| 1.5 wording that never changes: the four stat captions, the Synergies heading and its empty line, Upgrade, Soon, Sell | Static text in `Hud.uxml`, as `Restart` is |
| 1.5 wording that is filled in | Text fields in the scene, on the panel view. Title: display name. Number format of a stat. Full value: amount. Refund value: amount. Refund tail: cost. |

Not in this plan:
- **The letter on the element tile.** It needs a letter and a letter colour per element, which exist nowhere. The header tile is drawn without it, as the tower button's swatch is and for the same reason: the name stands beside it. It arrives as two palette fields with the first tile that stands alone.
- Rows for known and locked synergies, the kind of shot, any working upgrade, a sell confirmation, a message for a sale.
- From the reviews of 4b: `ElementChart.Set` accepting +Infinity (its file is not touched), and the camera kept off for the first two frames after a load (the framer is not touched; see Risks).
- A WebGL template whose canvas fills its frame, and a larger canvas (see "The canvas").

## The canvas
Today's canvas is 960 x 600 (`defaultScreenWidthWeb`, `defaultScreenHeightWeb`; the Default template). The panel scales with match 0.5 on a 960 x 600 reference, so:

| Canvas | Column | Cell on screen | Inside the column, reference px |
|---|---|---|---|
| 960 x 600 | none (4b) | 50.4 px | – |
| 960 x 600 | yes | 44.8 px, width-bound, 27 px unused above and below the board | 460 |
| **960 x 540** | yes | 44.8 px, height-bound | 429 |
| 1280 x 720 | yes | 59.7 px | 429 |

**Decision: 960 x 540.** At 960 wide the cell is 44.8 px at either aspect, so 16:9 does not win the cell back; only a wider canvas does. 16:9 is chosen because it is the shape of most screens in full screen and the one where the column is shortest, so the panel is judged in its worst case; it also draws 10 percent fewer pixels. 1280 x 720 gives larger cells than today for 1.6 times today's pixels and a page that needs a window of about 1300 x 800: a WebGL performance and page choice for Eduardo, and two numbers to change.

For the itch.io page later: the embed's viewport size is typed on the project page and must equal the canvas, 960 x 540, plus the height of the Default template's footer if that is kept (itch.io does not read the size from a Unity build reliably). Whether 960 fits the page's column is checked when the page is made. The HUD frames the board at any size, so full screen needs nothing more.

## Tasks
Order and editor use: plan 0007, "Editor sequencing", with `tools/unity-lock.sh` around every batch run (`docs/unity-cli-notes.md`). T4c-1 to T4c-3 are code only and run in parallel on the signatures below; no balance task.

### T4c-1 – Core and Gameplay: the sell value and the selection
Serves 4c-AC1 to AC5 and AC11. Code only.

```csharp
// Battle
// What TrySellTower would pay for the tower on the cell now. False, with 0, when the cell has no tower. Changes nothing.
public bool TryGetSellValue(GridPos cell, out int gold);

// TowerPlacementInput
public Tower SelectedTower { get; }      // null: none selected, it was sold, or the run is not running
public void Deselect();                  // ends the selection; nothing else changes
public bool SellSelected();              // Battle.TrySellTower on its cell, then Deselect; false with none selected
```

- `Battle.TrySellTower` removes the tower and adds what `TryGetSellValue` gave; the refund expression exists once.
- `SelectedTower` holds the Core `Tower` and answers it only while the battle is running and `Board.TowerOn` of its cell is that same object.
- `Click`: nothing when the pick fails; a cell with a tower selects it; otherwise, with a tower selected, `Deselect` and return; otherwise buy as today. The call to `TrySellTower` in `Click` goes.
- `Select(int)` also ends the selection when the index is in the list. `Update` calls `Deselect` when Escape was pressed this frame.

**Done when:** Core has no `UnityEngine`; Gameplay mentions no view, material or colour; `Update` has no `new`.

### T4c-2 – Presentation: the frame and the disc on the board
Serves 4c-AC5, AC6 and AC11. Code only; `BoardView.cs`.

- A serialized `selectionFramePrefab`, authored for a cell size of 1: one instance named `SelectionFrame` made in `Start`, scaled by the cell size, inactive.
- `LateUpdate` reads `placementInput.SelectedTower` once. The frame is active while it is not null and moves only when it is another tower. The disc is active while a tower is selected or the hover shows, and stands for the selected tower's cell, or else for the hovered cell as today. The hover follows `HasHoverCell` alone.
- Review note taken: the unreachable `if (selected == null) return;` goes.

**Done when:** no per-frame code allocates, and a position, a scale or a material is written only when it changes.

### T4c-3 – Presentation: the column and the panel
Serves 4c-AC4, AC5, AC7 to AC9 and AC11. Code and `Hud.uxml`; in parallel with T4c-2.

- `Hud.uxml`: a row `board-row` between the bars holds `board-area` and `side-column`; `tower-bar` moves from `bottom-bar` into `side-column`, beside a new `tower-panel`:
  - `tower-panel-tile` and the label `tower-panel-title`;
  - four rows of class `stat-row`, each a caption and a value label: `stat-element`, `stat-damage`, `stat-shots`, `stat-range`;
  - `synergies-heading` and `synergies-empty`;
  - `upgrade-button`, a plain element with two labels that ignores picking;
  - the `Button` `sell-button`, not focusable, holding its caption and the labels `sell-value` and `sell-value-tail`.

  Only the buy buttons, Sell and the end screen take picking. `Hud.uss` gets only what keeps these in place and shows `tower-panel` in place of `tower-bar` while `side-column` has the class `side-column--tower`; T4c-4 styles them.
- New `TowerPanelView.cs`: a serialized document, placement input, palette, bootstrap and the five texts of the table above. In `LateUpdate`:
  - When `SelectedTower` is not the tower shown: it switches `side-column--tower`; for a tower it writes the title, the tile's background (the colour of its look's tower material), the element name with its UI colour as the label's colour, and the three numbers through the number format with the invariant culture; it removes the drop classes.
  - With a tower selected it reads `TryGetSellValue`. When the value is not the one shown: it writes `sell-value` (the full text at the cost, the refund text below it) and `sell-value-tail`, and `sell-button` has `sell-button--refund` while the value is below the cost. If the tower was already shown, `sell-button` also gets `sell-button--pop`; on the view's next frame that class gives way to `sell-button--settle`, which stays until the selection changes.
  - `sell-button.clicked` calls `SellSelected`.
- `TowerBarView.cs`: its summary names the side column.

**Done when:** the view holds no rule, no number, no colour and no text of its own, and computes no refund.

### T4c-4 – Assets, looks, scene and player settings
Serves 4c-AC6 to AC10. Editor open on the worktree; after T4c-1 to T4c-3 and the stage 4 review. Through `unity command`, except `Hud.uss`:
- `Presentation/Materials/SelectionFrame.mat` and `Presentation/Prefabs/SelectionFrame.prefab`: four bars as section 1.5 states, with no collider and no shadow.
- `Hud.uss`, section 1.5: `board-row`, the column, the tower button's size and spacing in it, the panel, Upgrade, Sell with its hover and pressed look, `sell-button--refund`, and the drop: `sell-button--pop` is the drop's first look with no transition; `sell-button--settle` carries the transitions of the number's scale and, after the direction's hold as a transition delay, of the number's and the border's colour. The rules that placed the buttons in the bottom bar go. Every default of the theme's button that the direction does not name is reset for Sell, as for the tower buttons.
- `Prototype.unity`: `selectionFramePrefab` on the board view; a `TowerPanelView` on `Hud` with its references and the direction's texts. Save the scene.
- Player settings: the default web canvas 960 x 540, through `script eval` (`PlayerSettings.defaultWebScreenWidth`, `defaultWebScreenHeight`), saved and checked in `ProjectSettings/ProjectSettings.asset`.

**Done when:** clean compile with an empty console; in Play the column holds both buy buttons and no cell lies under it; a click on a tower shows the frame, the disc and the panel with that tower's values; Sell shows the cost, then the refund with the drop once the tower has fired, and a click on it sells; Escape, a click on a free cell and keys 1 and 2 close the panel; everything is saved and `.vscode/settings.json` is reverted.

### T4c-5 – WebGL build
Serves 4c-AC10 and AC12. As T3b-5 of plan 0005.

## Tests for the qa-tester
On the worktree with its editor closed. A test not listed under "Tests that change" that fails is reported before it is changed.

**Tests that change**
- `SceneTestBase`: a helper that sells a cell (click it, check it is selected, `SellSelected`). `RunReferenceActionsAt` uses it for its sale rows; the gold after every row stays the document's. `RealAssetRunTests` sells through `Battle` and passes untouched.
- `PrototypeSceneTests` 2b-AC1/AC2: the second click selects; the removal and the full refund go through the helper.
- `GoldSceneTests`: both sales (before and after firing) go through the helper.
- `ElementSceneTests`: the two 4b-AC6 and 4b-AC7 tests that sell by a click use the helper; the 4b-AC7 button width and the 4b-AC16 place of the buttons become the column's; the 4b-AC8 test adds the column, Sell and Upgrade to the places with no hover cell; `AssertLayout` adds that the column covers no cell; the 4b-AC14 test adds `TowerPanelView`.
- `RunSceneTests` 3b-AC6, after a win and after a defeat: a click on a tower also selects nothing.

**EditMode**
- **4c-AC1:** the value is the cost before the first launch and `cost * percent / 100` after it, for percents 0, 50 and 100 and a cost that rounds down; false with 0 on a free cell, a wall and off the grid; asking changes neither the gold nor the board; on every tick of a run with launches, `TryGetSellValue` then `TrySellTower` raises the gold by exactly the value; the value falls on the tick of the first launch, also at an immune enemy; `Is.Not.AllocatingGCMemory`.
- **4c-AC10:** the player settings hold 960 and 540 for the web canvas.
- **4c-AC11:** source tests: outside `Core/` and `Tests/` only `TowerPlacementInput.cs` calls `TrySellTower`, and no file under `Presentation/` contains `RefundPercent`. The determinism test of 3b-AC10 passes untouched.

**PlayMode**, in `Prototype.unity`
- **4c-AC2, AC3:** a click on a tower selects it with the same gold and towers; another tower takes the selection; the same tower keeps it. Each way of ending: Escape, through a queued key with the input enabled; a click on a free cell, a wall, the spawn and the exit (no tower placed, the same gold, no message; the next click on a free cell buys); key 1 and key 2, also the one already chosen, and `Select` of each index; a sale. A click off the grid, key 3 and a wave starting keep it. `SelectedIndex` is the same before and after a selection that Escape ends.
- **4c-AC4:** `ClickButton` on Sell removes the tower, pays, ends the selection and brings the buy buttons back; with the pointer at the centre of Sell and of Upgrade, `Click` on the board input does nothing; Upgrade is no `Button` and ignores picking.
- **4c-AC5:** with a tower selected, tick to a defeat: on the next frame `SelectedTower` is null, the column shows the buy buttons under the end screen, the frame and the disc are inactive; a click on a tower selects nothing. After a restart nothing is selected.
- **4c-AC6:** selected: the frame is active on the tower's cell centre with the direction's size, height and material; the disc has that tower's range and material with the pointer on a free cell, on another tower, off the grid and over the column; the hover is on the hovered cell. Shadow selected to buy and a Light tower selected: the disc is Light's. After the selection ends the disc follows the hover again with the tower to buy (4b-AC17).
- **4c-AC7:** at the game view's aspect and at 1 and 2.5: `AssertLayout`; the column's rectangle lies between the bars and misses the board's; exactly one of `tower-bar` and `tower-panel` is displayed, before and after a selection; the buttons hold what 4b-AC7 says; the column and the buttons resolve to the direction's sizes.
- **4c-AC8:** for a Light and for a Shadow tower: the title, the four values (the asset's damage, shots per second and range, as the direction writes them), the tile's colour and the element label's colour; the captions, the heading and the empty line read the direction's wording and resolve to its styles. With the thread's culture set to one that writes a decimal comma, the values are unchanged.
- **4c-AC9, Eduardo's case, one test:** buy a tower beside the route, click it, `Hover` at the centre of `sell-button` and never call `Hover` again. Then one `Battle.Tick()` per frame, and on every frame of the test the number equals what `TryGetSellValue` answered after the last tick.
  1. Before the first launch: the full text for the asset's cost, the tail not displayed, no `sell-button--refund`, the border and the number resolve to the full look.
  2. On the first frame after the tick that lowers the value: the refund text (the asset's 20), the tail with the cost, `sell-button--refund` and `sell-button--pop`, the number resolved to the drop's colour and scale.
  3. On the next frame `sell-button--pop` is gone and `sell-button--settle` is on; after the direction's whole drop time, in real seconds, the number and the border resolve to the refund look at scale 1.
  4. `ClickButton` on Sell: the gold rises by exactly the number last shown, not by the cost; the tower is gone and the buy buttons are back.
- **4c-AC9, others:** the same drop with the pointer off the UI; a tower selected after it fired shows the refund look with neither drop class; selecting a never-fired tower after a fired one shows the full look and clears `sell-button--settle`; no class and no text is written on a frame where the value did not change.
- **4c-AC11:** the 4b-AC14 measurement with a tower selected and the panel open, for the input's `Update`, the board view and the panel view.
- **4c-AC12:** the scene tests of 2b to 4b pass with the changes above, the reference order's win on its tick with its gold included.

**Orchestrator in the browser, then Eduardo**
- The canvas is 960 x 540. Are the cells, at about 45 px against 50 before, still easy to read and to hit? If not, the answer is a larger canvas ("The canvas").
- A click on a tower: the frame, the range and the panel appear together, and the panel says what the tower is. A click on the ground closes it and buys nothing; the next click buys. Does that first click feel lost?
- Rest the pointer on Sell of a new tower as the first enemy comes into its range: the number falls with the drop while the mouse is still, and the click pays the lower value.
- A real click on Sell sells once and neither buys nor selects anything under it. Upgrade reads as planned, not broken.
- Escape, keys 1 and 2 and a click on another tower behave as written. In full screen the browser takes Escape to leave it.
- No cell lies under the column, on the canvas, in full screen and while the window is resized. On the first frames of a run and after Restart, does the board jump sideways?
- A tower selected in the last two columns: its disc lies behind the panel's text. Is the panel still read?
- The panel closes when the run ends. The console shows no error.

## Risks
- **A frame between the number and the click.** The label shows the value after the last tick of the drawn frame; a click is paid by Core when it is handled. If the tower fires in the frame of the click, Sell pays the refund while the last drawn number was the cost. Nothing closes that gap; the sale always pays Core's value.
- **The first frames after a load.** The framer still fits the whole screen until the HUD has a layout (plan 0007). With the column the board then moves about 100 px to the left, where it only changed size before. If the browser check sees it, the fix is the one plan 0007 names (the camera off until the area has a size, the pick refusing meanwhile): one task on the framer and the input.
- **The one-frame pop.** The drop relies on a USS transition starting from a look that was resolved for one frame. If the engine skips it, the view holds `sell-button--pop` for two frames; nothing else changes.
- **Transition delay and scale in USS.** Both are supported properties; if the delay misbehaves in the browser, the hold becomes a time on the view, as the message line has one.
- **A press on the column.** The board input sees every press. It is harmless only because a pick outside the grid changes nothing: a rule that ends the selection there would cancel every sale.
- **Height of the column.** At any 16:9 size the panel has 429 reference px, not the 488 the direction counts. This step needs about 262; the later full panel of 402 fits, a sixth stat row does not.
- **The disc under the column.** The column has no background and the selected tower's disc stays on screen, so near the right edge it lies behind the panel's left third.
- **Decimal comma.** On a machine with a Portuguese locale a plain `ToString` writes `1,5` in the editor and `1.5` in the browser; the invariant culture is part of 4c-AC8.
- **Buttons in a batch run.** As in plan 0007: a real mouse click on Sell is checked in the editor and in the browser only.
- **Escape in the browser.** In full screen the browser uses it to leave full screen and may not pass it on; a click on the ground still ends the selection.
- **WebGL performance and GC.** Per frame with a tower selected: one array read for the selection in each of two views, one integer query, two comparisons. Texts and classes change when the selection or the value changes: a few strings per selection and one on the drop.
- **Coupling.** The panel view reads the input, the battle and the palette, as the tower bar does. The board view and the panel view both poll `SelectedTower`; neither knows the other.
