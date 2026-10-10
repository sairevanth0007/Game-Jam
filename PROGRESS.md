# Progress

## How to resume
1. `git checkout lumenwave-25d` (never another branch). Read PLAN.md (architecture + milestones) and this file.
2. `powershell -File scripts/compile.ps1 -Setup` → must print `COMPILE OK`.
3. `powershell -File scripts/test.ps1` → EditMode + PlayMode results.
4. `powershell -File scripts/build.ps1` → `Builds/Windows/LumenWave.exe` + `Release/LumenWave-Windows.zip`.

## Done
- M0: branch, .gitignore, layers, URP Universal Renderer (Forward+), code-built input actions with rebinding persistence, asmdefs (Runtime/Editor/EditMode/PlayMode), scripts (compile/test/build/screenshot), core contracts (LightMath, LightRules, Harmony, GameEvents, LevelData/Builder/Runtime, BeatClock, PlayerHealth, Resonance, MaterialLibrary), 19 EditMode tests passing.

## Next
- M1–M4 in parallel workers (see PLAN.md ownership table).

## Known issues
- Windows MCP not available in session → visual checks via screenshot mode.
