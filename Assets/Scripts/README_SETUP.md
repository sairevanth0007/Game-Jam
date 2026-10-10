# LumenWave – Setup

All controls go through the Unity Input System asset `Assets/Scripts/Input/LumenControls.inputactions`.
No script reads keys or devices directly, so keys and devices can change without touching gameplay code.

## Input actions

| Map    | Action   | Keyboard                    | Gamepad                 |
|--------|----------|-----------------------------|-------------------------|
| Player | Move     | A / D, Left / Right arrows  | Left stick, D-pad       |
| Player | Jump     | Space, W, Up arrow          | South (A / Cross)       |
| Notes  | NoteLow  | J                           | West (X / Square)       |
| Notes  | NoteMid  | K                           | North (Y / Triangle)    |
| Notes  | NoteHigh | L                           | East (B / Circle)       |
| Game   | Pause    | Esc                         | Start                   |
| Game   | Respawn  | R                           | Select                  |
| Game   | Confirm  | Enter, Numpad Enter         | –                       |

Control schemes: `Keyboard` and `Gamepad`.

## Wiring the scripts (drag these into the fields)

Expand `LumenControls.inputactions` in the Project window to see its actions, then drag each one into the field.

| Script           | Field            | Drag in                    |
|------------------|------------------|----------------------------|
| PlayerMovement   | Move Action      | `Player/Move`              |
| PlayerMovement   | Jump Action      | `Player/Jump`              |
| NoteInput        | Note Low Action  | `Notes/NoteLow`            |
| NoteInput        | Note Mid Action  | `Notes/NoteMid`            |
| NoteInput        | Note High Action | `Notes/NoteHigh`           |
| InputRebinder    | Actions          | the `LumenControls` asset itself |
| InputRebinder    | Target Action    | whichever action that settings row rebinds |

### Player GameObject

1. Add `Rigidbody2D` (Dynamic). Rotation freeze, interpolation and continuous collision are set by the script.
2. Add a `CapsuleCollider2D` (or `BoxCollider2D`).
3. Add `PlayerMovement`, drag in Move and Jump.
4. Set **Ground Layers** to the layers you can stand on (e.g. `Ground`, `ShadowPath`). Don't include the player's own layer.
   The yellow box gizmo (select the player) shows the ground check; it turns green when grounded in Play mode.

### Notes

Put one `NoteInput` in the scene (e.g. on a `GameSystems` object) and drag in the three note actions.
Other scripts use `NoteInput.Instance`:

```csharp
NoteInput.Instance.NotePressed  += note => { /* note started */ };
NoteInput.Instance.NoteReleased += note => { /* note stopped */ };
bool held = NoteInput.Instance.IsHeld(Note.Low);
```

## How to change a key

1. Double-click `Assets/Scripts/Input/LumenControls.inputactions`.
2. Select the action (e.g. `Notes > NoteLow`), then the binding under it (e.g. `J [Keyboard]`).
3. In the right panel, click **Path** and pick a new control (or press **Listen** and press the key).
4. Click **Save Asset** (or enable Auto-Save) at the top of the window.

To add another key to the same action: click **+** next to the action → **Add Binding**, set its path,
and tick the right control scheme (`Keyboard` or `Gamepad`).

Players can rebind at runtime through `InputRebinder` (saved to PlayerPrefs, loaded on start; `ResetAll()` restores the defaults).

## How to add a new device

**The Input System supports it** (gamepads, joysticks, many HID devices, touch, pen):
just add a binding for it in `LumenControls.inputactions`, as above. No code needed.

**The Input System doesn't support it** (mic pitch, a MIDI library, OSC from a phone, a custom serial controller):
write an adapter, starting from a copy of `Assets/Scripts/Input/InputSourceTemplate.cs`. An adapter only calls:

- `NoteInput.PressNote(note)` / `NoteInput.ReleaseNote(note)` – every press needs exactly one release.
  Presses are counted per note, so the keyboard and an adapter holding the same note don't cancel each other.
- `PlayerMovement.SetExternalMove(Vector2)` / `ClearExternalMove()` – overrides the Move action while set.
- `PlayerMovement.ExternalJump(true / false)` – jump button down / up, works alongside the Jump action.

Release everything in the adapter's `OnDisable` (the template already does this).

## InputRebinder (settings menu without code)

One `InputRebinder` per settings row:

- Set **Actions** (the LumenControls asset), **Target Action**, and **Target Binding Index**.
- Point a UI Button's `OnClick` at `InputRebinder.StartRebind()`.
- Wire **On Rebind Finished (string)** to a text label to show the new key.
- Esc cancels a rebind (change it with **Cancel Control Path**).

Binding indices count composite parts too:

| Action                | Index → binding |
|-----------------------|-----------------|
| Move                  | 0 AD Keys (composite, can't rebind), 1 A, 2 D, 3 Arrow Keys (composite), 4 ←, 5 →, 6 Left stick, 7 D-pad |
| Jump                  | 0 Space, 1 W, 2 ↑, 3 South |
| NoteLow / Mid / High  | 0 J / K / L, 1 West / North / East |
| Pause                 | 0 Esc, 1 Start |
| Respawn               | 0 R, 1 Select |
| Confirm               | 0 Enter, 1 Numpad Enter |

## Notes for the team

- Actions are shared: if a script disables an action in `OnDisable`, it is disabled for every script using it.
- `LumenControls.inputactions` has **Generate C# Class** on (`LumenWave.LumenControls`). Unity creates
  `LumenControls.cs` on first import. The first person to open the project should commit it with its `.meta`,
  so everyone gets the same GUID. Nothing depends on the generated class; scripts use `InputActionReference`.
- `Assets/InputSystem_Actions.inputactions` (Unity's default asset) is still set as project-wide actions. Our scripts don't use it.
