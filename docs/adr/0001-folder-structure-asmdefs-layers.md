# ADR 0001 – Folder structure, asmdefs and layers

Status: Accepted (prototype slice 1). Amended by ADR 0002, which adds the Presentation layer, by ADR 0003, which adds the simulation tick and pooling, and by ADR 0004, which pools enemies. The flow field moved from four to eight directions in slice 3c (plan 0006).

## Context
The project has no game code yet. Slice 1 (a grid and one enemy crossing it with pathfinding) needs a home for pure rules, data assets, Unity glue and tests. CLAUDE.md section 7 sets the layer rules and the WebGL performance rules. This ADR fixes only what slice 1 uses and records what is deferred.

## Decision
All game content lives under `Assets/_Game/`. The root namespace is `ElementalTD`.

| Folder | Assembly | References | Holds |
|---|---|---|---|
| `Core/` | `ElementalTD.Core` | none; `noEngineReferences: true` | Pure C# rules: grid, flow field, path following, the board and the battle tick (ADR 0003). |
| `Data/` | `ElementalTD.Data` | Core | ScriptableObject definitions and their `.asset` files (`Data/Assets/`). Each definition converts to Core types. |
| `Gameplay/` | `ElementalTD.Gameplay` | Core, Data | Thin MonoBehaviours that wire Core to Unity. No rules here. |
| `Presentation/` | `ElementalTD.Presentation` | Core, Data, Gameplay | Visuals and UI; see ADR 0002. |
| `Scenes/` | – | – | Game scenes. |
| `Tests/EditMode/` | `ElementalTD.Core.Tests` | Core, Data, Test Framework; Editor platform only | EditMode tests of Core and of the Data conversions. |
| `Tests/PlayMode/` | `ElementalTD.PlayModeTests` | Core, Data, Gameplay, Presentation, Test Framework | PlayMode smoke tests of scenes. |

Dependencies point inwards: Core knows no other layer, Data and Gameplay know Core, and nothing references Gameplay except Presentation and the PlayMode tests.

Pathfinding is one **flow field**: shortest distances from the exit over an 8-neighbour grid, stored as one whole-number array. A straight step costs 408 and a diagonal one 577, so 577/408 stands for √2 and no float decides a route. A diagonal step needs both cells it passes between to be free. Every enemy reads the next cell from it. A spawn cell with infinite distance means the path is blocked.

`com.unity.test-framework` is a direct dependency in `Packages/manifest.json`, so that the tests do not hang on a transitive dependency.

### Deferred section 7 rules
| Rule | Status | Comes back when |
|---|---|---|
| Object pooling | **Applies.** Projectiles from slice 2c (ADR 0003), enemies from slice 3a (ADR 0004). | – |
| GPU instancing | Deferred. A few hundred primitives with a shared material batch through the SRP Batcher; slice 3a has about twenty enemies at most (ADR 0004). | A browser measurement shows the enemy peak costing frames. |
| Fixed-tick logic | **Applies from slice 2c** (ADR 0003). | – |
| Modifier system | Deferred. Enemy and tower stats are read directly from data. | The first modifier (slice 5 perks, or Slowed). |
| Seeded, injectable RNG | Not needed. Nothing is random yet. | The first random draw (slice 5 perk options). |
| Zero allocations in `Update` | **Applies now.** Arrays are allocated once; no LINQ, boxing or closures in the movement path. | – |

## Alternatives
- **A\* per enemy.** About the same code, but it must be rerun per enemy and replaced by a flow field when enemies multiply. Rejected.
- **Free-angle paths around inflated towers.** Much more code, and harder to keep deterministic and cheap on WebGL. Rejected for the prototype.
- **Float step costs with √2, or the whole numbers 10 and 14.** A last-bit difference between Mono and IL2CPP could turn a tie into another route; 10 and 14 make five diagonals tie with seven straights that are shorter. Rejected.
- **A single game assembly.** Faster to start, but Core could no longer be kept free of `UnityEngine` by the compiler. Rejected.
- **Create Presentation in slice 1.** It would have needed an event or interface boundary with nothing real behind it. Deferred to slice 2 (ADR 0002).

## Consequences
- The compiler enforces that Core has no `UnityEngine` reference, so Core is testable in EditMode without scenes.
- Core uses its own small value types (such as `GridPos`) instead of Unity's; `MapDefinition` converts them to world positions.
- Each deferred rule has a named trigger; the slice that hits the trigger plans it.
- `manifest.json` pins Cinemachine 3.1.7, but the editor resolves the built-in 6.6.0; this does not matter until something uses Cinemachine.
