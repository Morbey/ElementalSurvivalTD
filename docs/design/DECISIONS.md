# Elemental TD – Design decisions (source of truth)

> Closed decisions and status only, no discussion. The Functional Brain updates this file when a topic closes.

## Platforms and style
- 3D with a top-down camera, in a stylised low-poly look (reference: Bad North).
- WebGL and desktop on equal footing. The first milestone is a free WebGL demo on itch.io, to validate the game.
- Assets: free CC0 packs (Kenney, KayKit) for the prototype and the demo. Synty POLYGON only if the demo is well received.

## Core
- A single modifier system over attributes. Targets: tower, element, hero and global (gold, XP, luck, number of perks, enemies per wave).
- Perks on level-up, through XP: three options, pick one, and the game pauses during the choice.
- Controlled randomness in perks: the player picks the starting element, the draw is weighted towards elements already owned, and the first levels always show at least one option that counters the scenario.
- Luck: 0 is a pure draw. More luck means higher rarity, more options and more elements that are strong against the scenario.
- Automatic waves on a timer, 5 to 20 s apart, with no manual pause. The screen always shows the next wave, with its enemy and element.
- Map: an open grid where the player builds a maze with towers. There is pathfinding, and flyers go in a straight line.
  - A tower can only be placed if the spawn and every living enemy can still reach the exit, and never on the spawn cell, the exit cell or a cell an enemy occupies. Otherwise the game refuses it and tells the player why.
  - Towers can be sold, so the player can rework the board freely. The refund is total until the tower fires its first shot and partial after that (value still open).
- Economy: gold per kill and interest at the start of each wave, with a cap that rises through talents.
- Heroes: data plus reusable abilities (tank, sorcerer, support). They fight on their own and can be repositioned.
- Talents between runs: trees per tower, per hero and for economy/luck. They are permanent modifiers saved in the profile.
- Enemies: data plus behaviours (fly, regenerate, turn invisible, split).
- Scenario: defines a subset of enemy elements, shown in the menu and on pause.
- 3D boss: phase 2, not essential. It only goes ahead after the demo validates the game.

## Elemental system
- Base elements (6): **Fire, Water, Earth, Air, Light, Shadow**.
- Main cycle: Water > Fire > Air > Earth > Water. Light and Shadow are strong against each other.
  - Damage is **x2** against the weak element and **x0.25** against the strong one.
- Secondary resistances (damage **x0.75**): Earth resists Fire, Fire resists Shadow, Air resists Light and Shadow resists Earth.
- Immunity to the own element is absolute and nothing breaks it, not even Cursed.
- Hybrids: there are 15 combinations of two elements, and each base element takes part in five. Some three-element hybrids appear late in the run, as rares.
- First batch of hybrids, with each base element appearing once:
  - **Nature** = Earth + Water
  - **Lightning** = Air + Light
  - **Hellfire** = Fire + Shadow
  - **Poison** (Water + Shadow) waits for the next batch.

## States
- **Burning** (Fire): constant damage over a short duration.
- **Cursed** (Hellfire): while the target burns, its resistances are halved (x0.25 becomes about x0.62 and x0.75 about x0.87).
- **Poisoned** (Poison): starts with low damage and keeps rising, lasts longer than Burning and deals heavy damage at the end. Strong against bosses and slow enemies.
- **Slowed** (Nature): reduces speed and has synergy with Poison.
- **Disoriented** (Lightning): the enemy stops and spins in place. Afterwards it is immune for a while, and on bosses the duration is shorter.

## Next steps

### Prototype slices
Each slice ends in something Eduardo can see or play.
- [x] **Slice 1:** a grid and one enemy crossing it with pathfinding. Includes the first ADR (folder structure, asmdefs and layers), limited to what this slice needs.
- [ ] **Slice 2**, in three steps that Eduardo accepts one at a time:
  - [ ] **2a:** a WebGL build of slice 1 that runs in the browser. It goes to a private itch.io page, rebuilt after every slice, once Eduardo provides the account.
  - [ ] **2b:** the player places and removes blocking towers with the mouse; the path recalculates, and an invalid placement is refused with a message. The enemy respawns at the spawn when it reaches the exit, so the maze can be tried continuously.
  - [ ] **2c:** towers shoot and kill; the enemy has health.
- [ ] **Slice 3:** waves, gold and selling towers.
- [ ] **Slice 4:** two elements, with x2 and x0.25 damage.
- [ ] **Slice 5:** level up through XP and pick one perk.

### After the prototype
- [ ] Define the 12 remaining hybrids and their states.
- [ ] List of perks, starting heroes and scenarios.
- [ ] Initial balance values: resistances, durations and XP curve.

### Later phase: Feedback pipeline (after the base game)
- [ ] **In-game capture:** a key opens a quick form that automatically saves the screenshot, logs, FPS and run state.
- [ ] **Backlog:** each report becomes a GitHub issue, classified as bug, improvement, performance or content.
- [ ] **Automatic non-production pipeline**, to try with LangGraph: triage, planning, implementation, tests and an automatic build on the private itch.io page.
- [ ] **Promotion to production** (the public game) only after Eduardo validates.

## Parking lot
Ideas raised mid-slice, one line each. Review them when a slice closes.
- An `art-director` agent for art direction and UX (element colours, top-down readability, HUD and perk screen, choosing CC0 assets). Revisit when slice 4 brings the first real visuals.
- Colour the hover highlight by whether a tower can be placed there (needs a path check per hovered cell).
