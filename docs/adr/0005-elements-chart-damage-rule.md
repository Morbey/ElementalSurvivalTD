# ADR 0005 – Elements: identity, the chart and the damage rule

Status: Accepted (prototype slice 4, step 4b). Amends ADR 0001 (the modifier system stays deferred) and ADR 0004 (a look per element). Amended by ADR 0006 (the kind of a hit is recorded).

## Context
Slice 4 brings the first two of the six base elements: Light and Shadow deal x2 to each other and nothing to their own. The other four bring x0.25 and x0.75, and hybrids follow. CLAUDE.md asks for elements as data and no balance number in code, and Core may not see a ScriptableObject. Three closed decisions shape the rule: a tower shoots the closest enemy even when it is immune (a talent changes that later), every hit will show its damage and what kind of hit it was (the step after this one), and a wave may mix elements.

## Decision
1. **An element is an asset with an id.** `ElementDefinition` holds a whole id from 1, unique among elements, and a display name. Core sees only the value `Element`, which wraps the id; its default is "no element". No code names an element.
2. **Towers and enemies carry their element in their stats.** An enemy takes it on entry and a shot copies its tower's at launch. A wave has no element of its own: its elements are those of the enemies in its groups (ADR 0004).
3. **The relations are one table.** `ElementChart.asset` lists the elements and, per ordered pair of attacker and defender, a damage multiplier; a pair it does not list deals x1. It converts to the Core `ElementChart`, which `Battle` receives. Light and Shadow are two rows today; x0.25 and x0.75 are more rows of the same kind.
4. **Immunity is a rule, not a row.** The chart answers 0 for a pair of one same element, and refuses to store such a pair or a multiplier at or below 0. So no data can break immunity, and a later rule that weakens resistances (Cursed) has no 0 to step around.
5. **One place decides a hit.** When a shot lands, `Battle` asks the chart once for the multiplier of the shot's element against the target's, and the target loses the shot's damage times it. That one number is also the kind of hit: 0 immune, above 1 strong, below 1 weak, 1 normal. It is read on landing, not at launch, so a state on the target can change it later.
6. **Targeting knows no element.** The talent that stops towers from attacking their own element will be a filter in the choice of target, on a multiplier of 0.
7. **No element** deals and takes x1 and is never immune. Core tests and simulations use it; no playable asset does.
8. **How an element looks belongs to Presentation.** One `ElementPalette` asset maps an element to its UI colour and to the materials of its towers, enemies, shots and range disc. Data holds no colour and no material. Views stay anonymous (ADR 0004): a pooled view takes the material of whatever it shows and changes it only when the element changes.
9. **The modifier system stays deferred.** A modifier changes an attribute of one target, has a source and can be removed. The elemental multiplier belongs to a pair of attacker and defender and is looked up per hit; nothing is added, stacked or removed. Its first real user is still slice 5 (ADR 0001). It will plug in on both sides of this lookup: the tower's damage before it, the target's resistances after it.

## Alternatives
- **An enum of elements in Core.** Readable, but every new element is a code change and Core would know the content. Rejected.
- **Ids given by the position in the chart's list.** No hand-written id, but a tower or an enemy could not convert to stats without the chart in hand. Rejected: ids are written on the asset, and the chart refuses two elements with one id.
- **Core element objects compared by reference, cached on the assets.** No chart to pass, but a cache on a ScriptableObject goes stale when an asset is edited between two plays, silently breaking the relations. Rejected.
- **Relations listed on each element asset.** The table would be spread over six files and still need a list of all elements. Rejected.
- **Immunity as a chart row of 0.** One rule less in Core, but a typing mistake breaks a rule the design calls absolute, and every later rule on resistances must treat 0 apart. Rejected.
- **The multiplier applied at launch.** The shot would carry its final damage, and a state gained by the target in flight could not count. Rejected.
- **Materials on `ElementDefinition`.** One asset per element, but Data would point at Presentation assets. Rejected.
- **A prefab per element, or one material tinted through a `MaterialPropertyBlock`.** A pool per prefab for a change of colour; the property block is rejected in ADR 0002. Rejected.

## Consequences
- A new base element is one `ElementDefinition`, its rows in the chart and one look in the palette, with no code.
- Hybrids and enemies that resist elements other than their own are not designed yet. If a hybrid is immune to its parts, rule 4 needs code.
- `Battle` takes the chart as an optional parameter; without one every pair deals x1 and immunity still holds.
- The hit log and the enemy's immunity mark are written where rule 5 computes the hit, and the chart names the kind (ADR 0006); the rule itself did not change.
- When towers and enemies get real models, the palette keeps the UI colours and the material swap gives way to a look per definition (ADR 0004).
