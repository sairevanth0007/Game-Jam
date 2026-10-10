# DEVLOG

## Sun 11 Oct 2026

**00:30 · M0 setup.** Created branch `lumenwave-25d` from `main`. Added a URP pipeline with a Universal Renderer (Forward+) next to the existing 2D one, and added physics layers. Input actions are built in code. Wrote the batchmode scripts (compile/test/build/screenshot) with a lock so parallel workers never open Unity twice.
- *Problem:* the first batch run seemed to take more than 10 minutes. *Cause:* `Start-Process -Wait` also waits for Unity's child processes (licensing client), which never exit. *Fix:* wait on the Unity PID only. A compile now takes about 15 s.
- *Problem:* rebinding overrides didn't survive a restart (EditMode test failed). *Cause:* code-built actions get new GUIDs every run. *Fix:* store overrides as `Map/Action|index|path`.

**01:00 · Architecture.** Levels are ASCII text plus entity lines. Scenes contain only a `SceneBootstrap`. Entity factories are discovered by attribute, so seven workers (visuals, player, light, sound, enemies, levels, UI) can build in parallel without touching each other's files.

**01:10 · MALink.** Wrote a hand-made OSC encoder and grandMA3 `Go+ Exec N` mapping, plus an F9 overlay. Verified it with a real UDP listener in an EditMode test.

**01:00–01:45 · First wave (7 parallel workers).**
- Umbra: kinematic controller with coyote time, jump buffer, variable jump, apex hang and dash. Procedural primitive rig with animation states and a verlet scarf.
- Light: raycast beams with mirrors (law of reflection) and prisms (per-channel dispersion). Exposure uses inverse-square plus Beer–Lambert, and is applied through `LightRules`.
- Sound powers: Echo bridge (a wavefront at 18 u/s that freezes into a walkable sine bridge), Chladni shield, finite-speed Pulse. All audio is synthesised: a drone that gains layers with each level, DSP-scheduled beat and arpeggio, SFX, and the finale harmonic chord.
- Enemies: Sentinel, Wisp, Echo Moth. 8 levels plus a hint-driven bot. UI: title, HUD, pause, settings with rebinding, credits, end screen.
- *Problem:* builds crashed with "level0 is corrupted". *Cause:* `SceneBootstrap` lived in `LevelRuntime.cs`. A MonoBehaviour in a scene must be in a file with a matching name, otherwise the script reference goes missing. *Fix:* moved it to its own file.
- **Verified:** 38 EditMode + 57 PlayMode tests pass, and the bot finishes all 8 levels with 0 deaths. Windows build is 94 MB, zip 34.6 MB.
