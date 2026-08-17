# Town Of Us — Role Reference

Every custom role's presentation metadata now lives in **one place**:

- `Core/RoleCatalog.cs` — display name, team, color, flavor description, and task-stats text.

Two systems read from that table:

- `Core/RolePresentation.cs` — the role name + color drawn **under player names** (in-world and in meetings).
- `Core/RoleInfoCard.cs` — the **task-stats text** shown on the Tasks tab ("Your Role: X — …").

To change anything, edit the matching line in `RoleCatalog.cs` (or tell me what you want and I'll edit it). Colors are stored as float RGBA (0.0–1.0); the hex below is the same color for reference.

## Crewmate roles

| Role | Color | Hex | Description | Task-stats text |
|---|---|---|---|---|
| Sheriff | `(0.95, 0.80, 0.20)` | `#F2CC33` | Shoot the impostors | Shoot Impostors. Shooting a non-enemy may kill you. |
| Engineer | `(0.20, 0.85, 0.95)` | `#33D9F2` | Use vents to move around the map. | Fix sabotages from anywhere and use vents. |
| Medic | `(0.30, 0.95, 0.55)` | `#4DF28C` | Protect one player from a kill. | Shield a player from one kill and learn the killer's clues. |
| Seer | `(0.65, 0.45, 1.00)` | `#A673FF` | Investigate players to reveal their faction. | Investigate other players to reveal their faction or role. |
| Vigilante | `(0.95, 0.65, 0.25)` | `#F2A640` | Shoot an Impostor; shooting a Crewmate kills you. | Shoot a player during meetings with limited shots. |
| Altruist | `(0.95, 0.50, 0.72)` | `#F280B8` | Revive a dead body — at the cost of your own life. | Revive a dead body — at the cost of your own life. |
| Mayor | `(0.35, 0.60, 1.00)` | `#5999FF` | Your vote counts double in meetings. | Your vote counts for the Vote Bank. |
| Swapper | `(0.45, 0.80, 0.30)` | `#73CC4D` | During meetings, swap the votes of two players. | During meetings, swap the votes of two players. |
| Spy | `(0.85, 0.70, 0.35)` | `#D9B359` | Get notified when someone vents or gets doused. | Get intel on venting and dousing around the map. |
| Investigator | `(0.35, 0.90, 0.75)` | `#59E6BF` | See the footprints of other players. | See the footprints of other players. |
| Time Lord | `(0.55, 0.65, 0.95)` | `#8CA6F2` | Rewind time to undo player movement. | Rewind time to undo player movement. |
| Snitch | `(0.95, 0.85, 0.35)` | `#F2D959` | Find the Impostors once your tasks are done. | Complete all your tasks to reveal the Impostors with arrows. |

## Impostor roles

| Role | Color | Hex | Description | Task-stats text |
|---|---|---|---|---|
| Assassin | `(0.95, 0.15, 0.18)` | `#F2262E` | During meetings, guess another player's role. A correct guess kills them; a wrong guess kills you. | Guess another player's role during a meeting. Wrong guesses kill you. |
| Janitor | `(0.55, 0.72, 0.95)` | `#8CB8F2` | Clean dead bodies so they cannot be reported. | Clean dead bodies so they cannot be reported. |
| Morphling | `(0.60, 0.90, 0.40)` | `#99E666` | Copy another player's appearance for a few seconds. | Morph into another player's appearance for a short time. |
| Camouflager | `(0.55, 0.60, 0.95)` | `#8C99F2` | Turn everyone grey so identities are hidden. | Turn everyone grey so identities are hidden. |
| Swooper | `(0.40, 0.45, 0.60)` | `#667399` | Become invisible for a short time. | Become temporarily invisible. |
| Underdog | `(0.90, 0.40, 0.40)` | `#E66666` | Faster kills when the Impostors are outnumbered. | Your kill cooldown is reduced while outnumbered. |
| Undertaker | `(0.45, 0.45, 0.75)` | `#7373BF` | Drag dead bodies away so they cannot be reported. | Drag dead bodies away so they cannot be reported. |
| Miner | `(0.85, 0.50, 0.20)` | `#D98033` | Mine vents that connect only to each other. | Mine vents that connect only to each other to move around the map. |

## Neutral roles

| Role | Color | Hex | Description | Task-stats text |
|---|---|---|---|---|
| Jester | `(0.86, 0.35, 0.95)` | `#DB59F2` | Get yourself voted out to win. | Get yourself voted out to win. |
| Executioner | `(0.45, 0.90, 0.85)` | `#73E6D9` | Get your target voted out to win. | Get your target voted out to win. If your target dies another way, you convert. |
| Arsonist | `(1.00, 0.45, 0.15)` | `#FF7326` | Douse players, then ignite them all to win. | Douse everyone, then ignite to win. |
| Phantom | `(0.75, 0.75, 0.85)` | `#BFBFD9` | Complete all your tasks after death to win. | Complete all your tasks after death to win. |
| Shifter | `(0.75, 0.55, 0.95)` | `#BF8CF2` | Swap roles and tasks with other players. | Swap roles and tasks with another player. Shifting an Impostor kills you. |
| The Glitch | `(0.45, 0.95, 0.35)` | `#73F259` | Mimic, hack, and kill everyone to be the last one standing. | Mimic players, hack them, and kill everyone to be the last one standing. |
