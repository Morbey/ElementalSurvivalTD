# ADR 0001 – Folder structure, asmdefs and layers

Status: Accepted (prototype slice 1). Amended by ADR 0002, which adds the Presentation layer.

## Context
The project has no game code yet. Slice 1 (a grid and one enemy crossing it with pathfinding) needs a home for pure rules, data assets, Unity glue and tests. CLAUDE.md section 7 sets the layer rules and the WebGL performance rules. This ADR fixes only what slice 1 uses and records what is deferred.

## Decision
All game content lives under `Assets/_Game/`. The root namespace is `ElementalTD`.

| Folder | Assembly | References | Holds |
|---|---|---|---|
| `Core/` | `ElementalTD.Core` | none; `noEngineReferences: true` | Pure C# rules: grid, flow field, path following. |
| `Data/` | `ElementalTD.Data` | Core | ScriptableObject definitions and their `.asset` files (`Data/Assets/`). Each definition converts to Core types. |
| `Gameplay/` | `ElementalTD.Gameplay` | Core, Data | Thin MonoBehaviours that wire Core to Unity. No rules here. |
| `Presentation/` | `ElementalTD.Presentation` | Core, Data, Gameplay | Visuals and UI; see ADR 0002. |
| `Scenes/` | – | – | Game scenes. |
| `Tests/EditMode/` | `ElementalTD.Core.Tests` | Core, Data, Test Framework; Editor platform only | EditMode tests of Core and of the Data conversions. |
| `Tests/PlayMode/` | `ElementalTD.PlayModeTests` | Core, Data, Gameplay, Presentation, Test Framework | PlayMode smoke tests of scenes. |

Dependencies point inwards: Core knows no other layer, Data and Gameplay know Core, and nothing references Gameplay except Presentation and the PlayMode tests.

Pathfinding is one **flow field**: a breadth-first search from the exit over a 4-neighbour grid with uniform cost, stored as one distance array. Every enemy reads the next cell from it. A spawn cell with infinite distance means the path is blocked.

`com.unity.test-framework` is a direct dependency in `Packages/manifest.json`, so that the tests do not hang on a transitive dependency.

### Deferred section 7 rules
| Rule | Status | Comes back when |
|---|---|---|
| Object pooling | Deferred. One enemy, reused when it respawns. | Slice 2c (projectiles) or slice 3 (waves), whichever spawns repeatedly first. |
| GPU instancing | Deferred. A few hundred primitives with a shared material batch through the SRP Batcher. | Slice 3, when the enemy count grows; measure first. |
| Fixed-tick logic | Deferred. Core exposes `Step(dt)`; Gameplay drives it from `Update`. Swapping the driver does not touch Core. | Slice 3 (waves on a timer), or the first balance simulation, whichever comes first. |
| Modifier system | Deferred. Enemy speed is read directly from data. | The first modifier (slice 5 perks, or Slowed). |
| Seeded, injectable RNG | Not needed. Slice 1 has no randomness. | The first random draw (slice 5 perk options). |
| Zero allocations in `Update` | **Applies now.** Arrays are allocated once; no LINQ, boxing or closures in the movement path. | – |

## Alternatives
- **A\* per enemy.** About the same code, but it must be rerun per enemy and replaced by a flow field when enemies multiply. Rejected.
- **A single game assembly.** Faster to start, but Core could no longer be kept free of `UnityEngine` by the compiler. Rejected.
- **Create Presentation in slice 1.** It would have needed an event or interface boundary with nothing real behind it. Deferred to slice 2 (ADR 0002).

## Consequences
- The compiler enforces that Core has no `UnityEngine` reference, so Core is testable in EditMode without scenes.
- Core uses its own small value types (such as `GridPos`) instead of Unity's; Gameplay converts them to world positions.
- Each deferred rule has a named trigger; the slice that hits the trigger plans it.
- `manifest.json` pins Cinemachine 3.1.7, but the editor resolves the built-in 6.6.0; this does not matter until something uses Cinemachine.
