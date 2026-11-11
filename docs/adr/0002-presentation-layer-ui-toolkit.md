# ADR 0002 – Presentation layer and UI Toolkit

Status: Accepted (prototype slice 2). Rule 3 applies from 2a; the assembly and the UI arrive in 2b. Amended by ADR 0003 (who raises events, who owns views, health bars) and by ADR 0004 (clicks on the UI).

## Context
ADR 0001 deferred the `Presentation` layer until the first real visual or UI. Slice 2 hits that trigger twice. The first WebGL build (2a) cannot use `GameObject.CreatePrimitive` or materials cloned at runtime: the player strips unused engine code, and the built-in default material is not a URP material. Slice 2b then adds the first UI (the placement refusal message) and the first visuals the player creates (towers and the hover highlight).

## Decision
1. **Layer.** `Assets/_Game/Presentation/` holds materials (`Materials/`), prefabs (`Prefabs/`), UI documents (`UI/`) and the view MonoBehaviours: board tiles, tower visuals, hover, camera framing and UI. Its assembly, `ElementalTD.Presentation`, references Core, Data and Gameplay, and is created with its first script.
2. **Boundary.** Core and Gameplay expose state as properties and changes as plain C# events; Presentation reads and subscribes. Gameplay never references Presentation and instantiates no prefab. ADR 0003 says which changes are events and which are polled.
3. **Visuals are serialized assets.** No `GameObject.CreatePrimitive` and no `new Material` at runtime. Each colour is its own material asset, so every renderer stays on the SRP Batcher.
4. **UI is UI Toolkit**: one `UIDocument` with one `PanelSettings`, UXML for structure and USS for style.

## Alternatives
- **uGUI with TextMeshPro.** The UI would live in scene and prefab YAML, which can only change through the live editor, and TextMeshPro needs its resources imported. UXML and USS are text files that agents write and review as diffs. Rejected.
- **IMGUI (`OnGUI`).** No assets, but it allocates every frame and cannot grow into the HUD or the perk screen. Rejected.
- **Gameplay calls Presentation through an interface.** An abstraction with one implementation; events give the same decoupling with less code. Rejected.
- **One material tinted with a `MaterialPropertyBlock`.** It takes renderers off the SRP Batcher. Rejected.

## Consequences
- The PlayMode test assembly also references Presentation.
- Presentation, not Gameplay, owns the camera framing and everything that only shows state, so Gameplay components stay testable without visuals.
- A click on the UI must not reach the board; ADR 0004 holds the rule.
