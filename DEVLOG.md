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

**04:50–05:15 · Bug hunt (RC4).** Fixed 6 confirmed player-facing bugs, each with regression tests:
- Dying during a level-change fade soft-locked the transition.
- Early respawn left the death fade dark.
- "Restart checkpoint" while dead caused a double teleport.
- The shield hum kept playing after pause or quit.
- The secret count carried over from level-select runs.
- The fullscreen toggle showed the wrong state.

Pause is now blocked during the finale. Tests 38 + 77 pass; flow test 0 fails; autoplay at 60 fps.

**07:50–12:05 · Visual overhaul (branch `lumenwave-visual`; RC4 untouched on `lumenwave-25d`).**
- **Assets (CC0, downloaded with curl, no login):**
  - Quaternius Universal Animation Library 1 + 2 (mannequin and 250+ humanoid clips).
  - Kenney Nature, Graveyard and Castle kits.
  - ambientCG rock, moss, ice and brick textures.
  - Poly Haven night skies.
  - GGBotNet fonts (OSerif, Jupiteroid).
  - Windows MCP still fails to connect, so there was no browser.
- **Hero:**
  - Umbra is the UAL mannequin with a Humanoid avatar and an Animator Controller.
  - Clips: a walk/jog/sprint blend tree on speed, then jump start, fall loop, land, dash, death and sing. Cast, shield and hurt are on an upper-body layer.
  - Root motion off, Foot IK, and footstep/land animation events for dust.
  - No code-driven limbs (user correction at 11:00). The first pass used the Quaternius Animated Human with procedural overlays; that was replaced.
  - Black silhouette with a key-side rim, eyes on the head bone, verlet scarf, dissolve on death, cyan dash afterimages.
- **Camera:** Cinemachine with a position composer (dead zone, look-ahead), a 3D confiner and impulse shake. It is closer, so Umbra is about 15 % of the screen. Depth of field on the far background.
- **World:**
  - Bevelled triplanar rock platforms whose interiors fade to black (Limbo-style), with moss, snow, ice or paving caps and roots.
  - Blurred foreground silhouettes, 3D Kenney mid- and background layers in height and distance fog, mist sheets.
  - Poly Haven sky, a moon with corona and god rays, light shafts under every lamp, fireflies, embers and snow.
  - A theme per level: forest, canyon, ruins, ice cavern, garden, clockwork with gears ticking on the beat, prism hall, fortress, dawn.
  - Prefabs for 66 props and the lamp post. Timeline drives the finale.
- **Enemies:** silhouette redesign (a hooded lantern-warden, a corona wisp, a winged moth, a horned flare).
- **Effects:** Echo bridge ripples and motes, Shield shimmer with hit ripples, Pulse ring with camera shake, enemy hit flashes, crystal shards.
- **UI:** TextMeshPro with SDF fonts (OSerif headings, Jupiteroid body), segmented health bar, resonance cost ticks, key-cap hints, and a title logo with a waveform over a live scene.
- *Problem:* the bot sometimes died in level 8 of the real build. *Cause:* a long jump landed after the next jump hint, and the hint's height check failed in mid-air, so the hint was skipped. It depends on sub-frame timing and on the beat phase at load. *Fix:* bot-only. Any-height hints (`any=1`) crossed in mid-air are kept until landing. Game code unchanged.
- **Verified:** tests 38 + 77 pass. Built player: all 10 levels completed by the bot with 0 deaths at 60 fps (p95 16.7 ms), flow test 0 fails (plus verify). Before/after screenshots are in `docs/images/before_after/`.
