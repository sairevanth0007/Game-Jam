# Decisions log

Format: time (Sun 11 Oct 2026) — decision — why.

- 00:35 — Branch `lumenwave-25d` from updated `main`. Other branches left untouched; stash@{0} kept.
- 00:35 — Existing files on `main` (Cainos pack, `Assets/Scripts/Light|Shadow`, 2D renderer, sample scenes) are **kept**, not deleted. LumenWave lives in `Assets/LumenWave/` with its own asmdefs, so it is isolated from them. Build settings only list LumenWave scenes.
- 00:36 — Windows MCP tools were not exposed in this session even after reconnect. Visual verification uses built-player screenshot mode (`scripts/screenshot.ps1`) and batchmode; will retry MCP later. Asset downloads via Brave not possible without MCP → hero and assets are procedural (CC0 downloads only if a direct, licence-clear URL works from the CLI).
- 00:40 — New URP pipeline asset with a **Universal Renderer (Forward+)** in `Assets/LumenWave/Settings`, assigned to Graphics + all quality levels. The old 2D renderer asset is left in place, unused.
- 00:40 — Input actions are **built in code** (`LumenInput`) rather than an `.inputactions` asset: diff-friendly, no editor import step. Rebinding overrides persist as `Map/Action|index|path` lines in PlayerPrefs because code-built actions get new GUIDs every run (the Input System JSON override format keys on GUIDs — caught by an EditMode test).
- 00:40 — Keys: Move A/D (+arrows, stick, d-pad), Jump Space/A, Dash LeftShift/B, Echo J/LMB/X, Shield K(hold)/RMB/LT, Pulse L/MMB/Y, Pause Esc/Start.
- 00:42 — Levels are ASCII text files in `Resources/Levels` + entity lines; scenes contain only a `SceneBootstrap`, all content is built at runtime. Reason: fully reproducible, no hand-written scene YAML, procedural meshes don't need asset serialisation, easy for workers to iterate in parallel.
- 00:42 — Entity factories are discovered by attribute (`[LevelEntity("type")]`) so each subsystem registers its own entities without editing shared files.
- 00:43 — Light rules as pure logic: White (R∧G∧B part) is neutral; overlapping Red+Green becomes Yellow (beat-gated damage); remaining Red burns, Green heals + refills, Blue lowers gravity. Darkness (total < 0.12) refills resonance.
- 00:44 — Music in D, just intonation (1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8). Echo = fifth, Shield = tonic, Pulse = major third. 100 BPM, beat on the DSP clock.
- 00:50 — `Start-Process -Wait` hangs on Unity's lingering child processes; scripts now wait on the Unity process only (with timeout).
