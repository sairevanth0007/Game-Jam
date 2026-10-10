# LumenWave level files

Every `.txt` file in this folder is a level, played in file-name order (`01_...`, `02_...`).
The game reads them at runtime, so you can edit a map and press Play. No scene editing is needed.
After **adding or removing** a file, open the game scene once (or run *LumenWave > Refresh Level List*) and save it,
so builds pick up the new list. In the Editor the list refreshes on every Play.

## File layout

```
// comment lines start with //
name: 1-1 First Shadow          shown on the banner and HUD
subtitle: Light burns.           second line of the level banner
melody: C4 D4 E4 G4              notes played by echoes, in pickup order (loops)
drone: 1                         background drone layers (later phase)
hint1: {Move} to move.           text for hint stone '1' ('1'..'9')
map:
....rows of the map, top row first....
```

In hint text, `{Move} {Jump} {Down} {NoteLow} {NoteMid} {NoteHigh} {Pause} {Respawn} {Confirm}`
are replaced with the player's current key or button, including rebinds.

One character is one 1x1 tile. Short rows are padded with `.`, and spaces count as empty.
The bottom row is y = 0. The level's edges are walls, and falling below the bottom costs a life.

## Legend

| Char | Meaning |
|------|---------|
| `.`  | empty |
| `#`  | solid ground (black silhouette, rim on exposed edges) |
| `-`  | thin one-way ledge (jump up through; Down + Jump drops through) |
| `P`  | player start (feet on the bottom of this cell) |
| `C`  | checkpoint: shadow rune (2 tiles tall, stands on the cell's floor) |
| `E`  | echo collectible |
| `D`  | level-end door (2 tiles tall, stands on the cell's floor) |
| `1`-`9` | hint stone, text from `hint1:` .. `hint9:` |

Reserved for later phases (ignored for now): `b r y` lamps, `B R Y` shadow-path anchors,
`^` light spikes, `W` wisp, `S` searchlight, `~` light pit.
