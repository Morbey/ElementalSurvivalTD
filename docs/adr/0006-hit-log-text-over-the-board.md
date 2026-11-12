# ADR 0006 – Moments as a polled log, and text over the board

Status: Accepted (prototype slice 4, step 4d). Amends ADR 0002 (text over the board), ADR 0003 (moments, the tick count), ADR 0004 (pop-ups) and ADR 0005 (the kind of a hit).

## Context
Step 4d shows every hit as a number over the enemy, the word "immune" over an enemy that towers waste shots on, and a placement refusal at the refused cell. Until now views only drew state they could read at any frame. A hit is a moment: it is gone when the tick ends, and a frame may run several ticks. ADR 0003 deferred Core events until a view needed the moment itself, and rejected a UI Toolkit element per health bar because it moved through layout every frame. The numbers are the first text that sits over the board, up to forty at once with a word on every enemy, on WebGL.

## Decision
1. **A moment is a record in a Core log, read by its count.** `Battle` keeps the last 64 hits in a fixed array and counts every hit since the run began. A view remembers the count it has read and reads the records after it, once per frame, however many ticks ran. The tick calls nothing outside Core and allocates nothing. When more hits land than the log holds before a frame reads them, the oldest are overwritten: only their numbers are lost, never their damage.
2. **A record is what the view needs and nothing it could get wrong.** The target's position on that tick, the damage dealt, the kind of the hit and the tick. The kind is decided where the multiplier is read (ADR 0005): the chart names it, and no view compares a multiplier or an element. A record points at no enemy, so a hit that kills still shows.
3. **What lasts is state on the Core object.** A hit on an immune enemy adds no record: the enemy remembers the tick of the last one. A view asks each enemy on the board, so the n-th view still shows the n-th enemy and keeps nothing that belongs to one (ADR 0004).
4. **The battle counts its ticks.** That count plus the fraction of a tick left over is the clock of everything that shows the simulation: a number ages in battle time, so it stands still while the battle does, runs with the game speed and is the same in a test that ticks by hand. Feedback on an input action keeps unscaled time.
5. **Text over the board is UI Toolkit labels moved by transform.** One element of the HUD document, above the bars and under the end screen, holds a fixed pool of labels that ignore picking. A view moves a label with `translate` and fades it with `opacity`, marked as a dynamic transform: neither goes through layout or rebuilds the text. Where a label stands comes from the camera, read twice per frame, not per label. A dead label is hidden by visibility, not by display. Sizes, colours and outlines are USS classes; a text or a class is written only when it differs from what the label holds, and the text of an amount is built once.

## Alternatives
- **Core events for a hit.** The tick would call into Presentation once per hit, and a view would need the enemy alive to place the number. Rejected, as in ADR 0003 and ADR 0004.
- **A buffer cleared every tick.** A frame that runs two ticks loses the first tick's hits unless the driver tells the views after each tick, which is an event again. Rejected.
- **The immune word kept in the view, by enemy.** The view would hold a map from enemy objects to labels and tell a reused object from the one it was shown for. Rejected: one whole number on the enemy needs neither.
- **TextMeshPro meshes in the world.** No layout at all, but a draw call per label on the platform where draw calls are scarce, vertex colours rewritten and uploaded for every fading label, the TextMeshPro resources imported into a `Resources` folder that ships whole, a second font beside the HUD's, and sizes the visual direction gives in panel pixels converted to world units on every resize. ADR 0002 rejected it for the HUD on the same asset grounds. It is the fallback if the browser measurement fails.
- **One mesh of digit quads drawn by hand.** The cheapest to draw, but a text renderer of our own for forty numbers. Rejected until a measurement asks for it.
- **One element that draws every number in its own `generateVisualContent`.** It rebuilds all the text every frame and has no outline. Rejected.
- **USS transitions for the rise and the fade**, as the sell value's drop uses. They run on the panel's own clock, so numbers would move while the battle stands still, a test could not tick them, and a pooled label needs a frame at its start look before it can be reused. Rejected.
- **A second, world-space UI panel.** Sizes would be world units and the panel one more thing to draw. Rejected.

## Consequences
- The cost was reasoned, not measured: step 4d measures it in the browser (plan 0009). Per frame it is one transform per label alive and one opacity per label that fades; per hit, a text or a class only when the pooled label held another.
- A gold pop-up, a death effect or a sound reads a log of this shape; none needs an event.
- A view that falls behind by more than 64 hits skips to the oldest kept. The view's own cap is below that, so nothing it could have shown is lost.
- A number is anchored to a point of the board and follows it through a resize; it does not follow the enemy.
- Short-lived text may lie over a cell and over a bar. It never takes a click, so the rule of ADR 0004 holds.
- `Battle.TickCount` is also the clock for any later view of the simulation.
