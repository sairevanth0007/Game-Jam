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

**01:55–02:45 · Second and third waves.**
- Visual polish: Umbra became a true black silhouette with a stencil-masked outline (no inner limb lines). Added a stone-face shader (bricks, cracks, gradient into darkness), a single moon, new camera framing (ground at about 25% from the bottom), damage sparks and heal motes.
- *Problem:* red beams looked orange under ACES tonemapping. *Fix:* more saturated red display colour and lower core HDR. The red mood was also desaturated so red hazards pop.
- Gameplay QA: all levels are beatable at 1× against the real beat clock with 0 deaths. Added checkpoints to long final stretches. Added the `-lw-autoplay` real-build verification.
- Content: levels expanded about 2× and two new levels added (Echo Canyon, Crystal Choir), for 10 in total, roughly 12–15 min for a human.
- Finale: Umbra raises their arms and sings. Six spectrum rings (radii 1:2:3:4:5:6) pulse on the beat, every beam and lamp turns warm white and breathes, enemies calm, the sky turns to dawn, then the ending text appears.
- *Problem:* a level whose scene isn't in the build silently loaded level 1. *Fix:* `SceneBootstrap.PendingLevelOverride`.
- **RC1 (02:45):** 108/108 tests pass. The real build autoplays level 1 to completion in 31.6 s with 0 deaths. Release zip is 34.6 MB.

**02:50–04:40 · QA and feel.**
- Deep QA on the real build:
  - Audio captured via an `OnAudioFilterRead` tap and analysed in Python. Spectral peaks sit on D just-intonation ratios, the envelope shows the 100 BPM beat, and nothing clips. Added +3.5 dB make-up gain.
  - Frame times: locked 60 fps.
  - Scripted menu flow: 23/23 pass. UI label wrapping fixed.
- Showcase mode to review every power. Fixed the mood picked by level number instead of level data, and the moon draw order.
- Added fades, level cards, assist mode and the beat indicator.
- *Problem:* a 100 ms hitch when the finale started. *Cause:* the finale chorus loops were synthesised on the main thread. *Fix:* pre-render them at level load.
- Flare enemy: mounted lantern firing bolts the shield absorbs (this was in the brief). Its housing was invisible against the night sky, so it got an emissive trim.
- End screen shows secrets x/total and best time; level select shows per-level bests.
- RC2 (03:39) and RC3 pushed with verified builds.
