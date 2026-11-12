# ADR 0003 – Simulation tick, pooling and combat views

Status: Accepted (prototype slice 2c). Amends ADR 0001 (deferred rules) and ADR 0002 (boundary, tower events, health bars). Amended by ADR 0004 (tick order, enemy pooling) and ADR 0006 (moments, text over the board).

## Context
Slice 2c adds damage, fire rates and projectiles. With a variable time step the number of shots in a pass depends on the frame rate, so the same maze could kill in the editor and leak in the browser. 2c also has the first objects that spawn repeatedly (projectiles) and the first world-space UI (a health bar), and the 2b review found that each enemy stepped itself and that tower changes were announced by the input component.

## Decision
1. **State and time are two Core classes.** `Board` holds what is on the board: the map, the flow field, one tower object per cell and the enemies on it, plus the placement rule. `Battle` holds time: `Tick()` advances everything by the constant `Battle.TickSeconds` (1/60 s) in a fixed order, listed in ADR 0004. Nothing else steps Core.
2. **One driver.** One Gameplay component accumulates `Time.deltaTime`, calls `Battle.Tick()` zero or more times per frame and exposes the fraction of a tick left over. Pause and game speed are `Time.timeScale`.
3. **Views interpolate.** Every moving Core object keeps its position at the previous tick. Views draw between the previous and the current position by the driver's fraction, in `LateUpdate`.
4. **Combat views poll; rare changes are events.** Presentation reads the `Battle` state each frame and draws it: the n-th pooled projectile view shows the n-th projectile. Only rare structural changes are plain C# events, raised by the Core object that changes: `Board.TowerPlaced` and `Board.TowerRemoved`. Feedback on an input action (`PlacementRefused`) stays on the input component.
5. **Presentation owns every view.** Gameplay instantiates no prefab; the enemy view moves to Presentation.
6. **Pooling.** `Battle` reuses its projectile objects through a free list. Presentation keeps a pre-warmed pool of projectile views that grows only when exceeded. Nothing is instantiated or destroyed per shot. A reused object counts its entries, and whatever points at it remembers the count: a shot aimed at an enemy that has since left the board is dropped, even when that enemy object is already back.
7. **Health bars are meshes.** Two flat quads on the enemy prefab (back and fill), each with its own URP Unlit material; the fill is scaled by the health fraction. The camera looks straight down and enemies do not rotate, so there is no billboarding.

## Alternatives
- **Variable step, as in slices 1 and 2b.** Less code, but shot counts change with the frame rate and tests could not promise what the player sees. Rejected.
- **`FixedUpdate` as the driver.** No accumulator to write, but it ties the game tick to the physics step and keeps the tick length in project settings instead of Core. Rejected.
- **No interpolation.** A 60 Hz tick against a 60 Hz display runs zero or two ticks on some frames, which shows as stutter. Rejected.
- **Core events for every spawn, hit and death.** Views would need a map from Core objects to views and the tick would call into Presentation. Polling needs neither while views are anonymous. A view that needs the moment itself reads a polled log (ADR 0006).
- **A UI Toolkit element per health bar.** It moves an element per enemy per frame through layout, which does not scale on WebGL. Rejected. Text over the board moves by transform instead (ADR 0006).
- **One bar material tinted through a `MaterialPropertyBlock`.** Rejected for the reason given in ADR 0002.

## Consequences
- Outcomes do not depend on the frame rate: within a platform, the same placements at the same ticks give the same result. The editor (Mono) and the browser (IL2CPP) may differ in the last bit of a float, which only matters for a borderline outcome. Balance simulations can call `Battle.Tick()` with no Unity code.
- Changing `TickSeconds` shifts balance slightly, so it is one constant in Core and not a setting.
- `Battle.Tick()` is part of the zero-allocation path, together with the view sync.
- If enemies start to rotate, the health bar must stop rotating with them.
