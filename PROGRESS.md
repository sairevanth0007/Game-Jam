# Progress

## How to resume
1. `git checkout lumenwave-25d` (never another branch). Read PLAN.md (architecture + milestones) and this file.
2. `powershell -ExecutionPolicy Bypass -File scripts/compile.ps1 -Setup` should print `COMPILE OK`.
3. `powershell -ExecutionPolicy Bypass -File scripts/test.ps1` runs EditMode + PlayMode (incl. bot playthroughs of every level at 1× and 2.5×).
4. `powershell -ExecutionPolicy Bypass -File scripts/build.ps1` produces `Builds/Windows/LumenWave.exe` and `Release/LumenWave-Windows.zip`.
5. Verification modes of the built player (all write PNGs/logs and then quit):
   - `-lw-screenshots <dir> [-lw-shots "level:x:y;..."]`: key-moment screenshots (`scripts/screenshot.ps1`).
   - `-lw-autoplay <dir> -lw-autoplay-level N [-lw-autoplay-seconds 60]`: the bot plays the real build and logs fps.
   - `-lw-flowtest <dir>` (then again with `-lw-flowtest-verify`): title → settings → play → pause → rebind → die → respawn → next level, plus rebinding persistence across restarts.
   - `-lw-showcase <dir>`: power and effect showcase shots.
   - `-lw-record-audio <file.wav>`: captures the mix for analysis.
   - `-malink ip:port[:prefix]`: enables grandMA3 OSC (F9 overlay).

## Milestones
M0 setup · M1 Umbra · M2 light system · M3 sound powers + audio · M4 enemies, levels, HUD, menus, bot · M5 polish · M6 verified Windows build + MALink. **All done.**
Release candidates pushed: RC1 02:45, RC2 03:39, RC3 04:45, RC4 about 05:15 (Sun 11 Oct 2026; bug-hunt fixes).

## Visual overhaul (branch `lumenwave-visual`)
- Release: `Release/LumenWave-Windows-visual.zip`. RC4 (`lumenwave-25d`, `Release/LumenWave-Windows.zip`) is the fallback.
- Hero: Quaternius UAL mannequin with Animator clips. Camera: Cinemachine. UI: TextMeshPro. Finale: Timeline. Prefabs for props and lamps. CC0 art from Kenney, ambientCG and Poly Haven. Everything is listed in CREDITS.md.
- Verify: `scripts/test.ps1`, `scripts/build.ps1 -Name visual`, `scripts/autoplay.ps1 -Name visual`, `scripts/screenshot.ps1 -Name visual`. Compare images with `tools/before_after.py`.
- The "Final status" section below describes RC4, before the overhaul. The hero, camera and UI described there were replaced as above.

## Melody shards + Lightkeeper mode (branch `lumenwave-visual`)
- **Melody shards:** one shard rests on each checkpoint lantern. The HUD shows "SHARDS x/N", each shard plays the next note of the level melody, and the exit stays sealed until all are gathered, then the full melody plays. The optional `shardskip:` header excludes off-route checkpoints (level 5). The finale level has no shards. Secret orbs stay optional. Code: `Level/MelodyShards.cs`, plus `ExitPortal` and `Hud`.
- **Lightkeeper mode** (title → LIGHTKEEPER, arena `Resources/Levels/lightkeeper_arena.txt`):
  - A reactive bot (`Lightkeeper/ReactiveBot.cs`) plays Umbra: it waits at beams, shields against red when it has resonance, and uses Echo over gaps.
  - The human runs 4 lamps (1–4 select, A/D or mouse move, Q/E aim, W on/off, R colour) under an energy limit (max 2 lit).
  - The Lightkeeper wins after 90 s or 3 bot deaths; there is a big result screen with Retry/Title.
  - OSC input is off by default (`-lk-osc <port>`; `/lamp/N/x|angle|on|color`), and lamp states mirror out via MALink when it's on. See docs/MALINK.md.
  - Screenshot: `docs/images/lightkeeper_arena.jpg`.
- **Verified (Sun 11 Oct 2026, ~15:35):**
  - Tests: 38 EditMode + 81 PlayMode, all pass, including the 3 Lightkeeper tests (idle lights → the bot reaches the door; a static lethal beam blocks it; OSC moves a lamp).
  - Built player: the bot completes levels 1–9 with all shards and 0 deaths. Zip rebuilt.
- **Known issues:**
  - In the arena screenshot the HUD's level label reads "LEVEL 01".
  - The two lit lamps' beams weren't visible in that frame. Lamp layout and energy were retuned after that screenshot and haven't been re-captured.
  - Nobody has played Lightkeeper by hand yet.

## Final status

### What works
- **Umbra:** black silhouette with a Schlick rim/outline, glowing eyes and a verlet scarf. Procedural animation (idle, run, jump, fall, land, dash, cast, shield, hurt, death, respawn, singing). Tight controls: coyote time, jump buffer, variable jump, apex hang, air dash.
- **Sound powers:**
  - Echo Path: a wave at finite speed that freezes into a walkable sine bridge, with wavelength λ = v/f.
  - Resonance Shield: a Chladni standing-wave dome that blocks red light, flare bolts, wisps and moths.
  - Pulse: a finite-speed ring that shatters crystals, stuns sentinels and flares, reveals hidden platforms and rotates mirrors and prisms.
  - Resonance refills in darkness and green light.
- **Light:**
  - Raycast beams with occlusion, inverse-square × Beer–Lambert intensity, mirrors (law of reflection), prisms (dispersion of white into R/G/B) and additive mixing (R+G behaves as yellow).
  - Colour rules: red burns or kills, green heals and refills, blue lowers gravity, yellow fires on the 100 BPM beat.
- **Enemies:** Sentinel (sweeping red beam), Wisp (light chaser), Echo Moth (hunts when you use powers), Flare (fires bolts the shield absorbs).
- **Levels:** 10 levels, about 12–15 min. First Light, Echo Path, Echo Canyon, Red Light, Blue Ascent, Crystal Choir, Beat Lasers, Prism and Mirror, Sentinels, and Harmony, a cinematic finale where Umbra sings harmonic rings 1:2:3:4:5:6 and every light turns harmonious. Checkpoints, tutorials and 1–3 secret shards per level.
- **UI:**
  - Title with level select and best times; HUD for health, resonance, timer, live key prompts and beat indicator.
  - Pause menu; settings with volumes, shake, fullscreen, assist mode, beat indicator and full keyboard/gamepad rebinding.
  - Credits, plus an end screen with time, deaths, secrets x/total and best time. Fades and level cards between scenes.
- **Audio:** fully synthesised in D just intonation. A drone gains layers with each level, beat pulse and arpeggio are scheduled on the DSP clock, SFX cover every event, and the finale has a harmonic chorus.
- **Look:** URP Forward+, bloom, ACES, vignette, grain, colour grading per level mood, parallax backdrop, stone-face shader, particles, follow camera with look-ahead.
- **MALink:** a hand-written OSC encoder sending grandMA3 `Go+ Exec N` commands per event. Off by default and safe offline, with an F9 overlay. See docs/MALINK.md.

### What is verified
- **Tests:** 38 EditMode + 77 PlayMode, all passing. They cover light math, rules, mixing, reflection, prism, Harmony, level parsing, input bindings and rebinding persistence, OSC encoding, and a real UDP listener receiving MALink packets.
- **Gameplay tests:** player feel; red damages; shield blocks; green heals; blue gravity; beat gating; echo bridge is walkable; pulse arrives at finite speed; enemies; flare bolts absorbed; checkpoints; finale; UI. A bot finishes **all 10 levels with 0 deaths at 1× and 2.5×**.
- **Real Windows build:**
  - Autoplay finishes levels at a locked 60 fps (p95 16.7 ms at 1600x900).
  - Flow test passes 23/23 and rebinding persists across restarts.
  - Audio capture: no clipping or silence, spectral peaks on just-intonation ratios of D, beat at 100 BPM.
  - No exceptions in player logs. Screenshots of every level, every power and the finale were reviewed (`Screenshots/`, `docs/images/`).
- **Release:** `Release/LumenWave-Windows.zip`, about 35 MB.

### Known issues
- Windows MCP was never exposed in this session, so no manual GUI play. Verification used the built player's autoplay, flow, screenshot and showcase modes instead.
- The bot times the beat better than a human. Assist mode (half damage and costs, wider safe beat window) is the safety net.
- The hero is procedural (primitive rig) rather than a downloaded CC0 model. Asset downloads weren't possible without the browser MCP, so no third-party assets are used.
- Rare single-frame hitches of up to about 33 ms when the finale starts.

### How to play
Unzip `Release/LumenWave-Windows.zip` and run `LumenWave.exe`. Controls are in README.md (A/D move, Space jump, Shift dash, J Echo, K Shield, L Pulse, Esc pause; gamepad supported).
