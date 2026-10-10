# Progress

## How to resume
1. `git checkout lumenwave-25d` (never another branch). Read PLAN.md (architecture + milestones) and this file.
2. `powershell -File scripts/compile.ps1 -Setup` → must print `COMPILE OK`.
3. `powershell -File scripts/test.ps1` → EditMode + PlayMode results.
4. `powershell -File scripts/build.ps1` → `Builds/Windows/LumenWave.exe` + `Release/LumenWave-Windows.zip`.

## Done
- M0: branch, .gitignore, layers, URP Universal Renderer (Forward+), code-built input actions with rebinding persistence, asmdefs (Runtime/Editor/EditMode/PlayMode), scripts (compile/test/build/screenshot), core contracts (LightMath, LightRules, Harmony, GameEvents, LevelData/Builder/Runtime, BeatClock, PlayerHealth, Resonance, MaterialLibrary), 19 EditMode tests passing.

- M1–M4: Umbra, light system, sound powers + audio, enemies (3), 8 levels, bot, UI, visuals. 95/95 tests pass, and the bot completes all levels. First Windows build is OK and its screenshots have been reviewed.
- MALink: OSC encoder + grandMA3 mapping + F9 overlay + UDP listener test (docs/MALINK.md).

## Next (02:00)
- Second wave: visual polish (solid Umbra silhouette, tile faces, camera framing, moods) and gameplay QA (1× bot fairness, soft-locks, hints, `-lw-autoplay` real-build verification).
- Then: final review build, docs with screenshots, release zip.

## Known issues
- Windows MCP not available in session → visual checks via screenshot mode.
